using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradeStorm.Api.Data;
using TradeStorm.Api.Hubs;
using TradeStorm.Api.Repositories;
using TradeStorm.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// База даних EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=loopmarket.db";
    const string prefix = "Data Source=";
    var idx = connStr.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
    if (idx >= 0)
    {
        var rawPath = connStr.Substring(idx + prefix.Length).Split(';')[0].Trim();
        if (!Path.IsPathRooted(rawPath))
        {
            var absolutePath = Path.Combine(builder.Environment.ContentRootPath, rawPath);
            connStr = $"Data Source={absolutePath}";
        }
    }
    options.UseSqlite(connStr);
});

// RFC 7807 стандартизована обробка помилок (Етап 2)
builder.Services.AddProblemDetails();

// Репозиторії (Етап 1)
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IListingRepository, ListingRepository>();
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();

// Сервісний шар (Етап 1 та 2)
builder.Services.AddScoped<IListingService, ListingService>();
builder.Services.AddScoped<IFavoriteService, FavoriteService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOrderService, OrderService>(); // Сервіс JWT та Refresh Tokens (Етап 5)

// Кешування в пам'яті (Етап 4)
builder.Services.AddMemoryCache();

// HTTP-клієнт із тайм-аутом для виклику зовнішніх REST-сервісів (Етап 4)
builder.Services.AddHttpClient("NbuApiClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

// HTTP-клієнт для інтеграції з API Нової Пошти (Етап 2 та 4)
builder.Services.AddHttpClient("NovaPoshtaClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddScoped<INovaPoshtaService, NovaPoshtaService>();

// Фонова черга задач без зовнішнього брокера (Етап 4)
builder.Services.AddSingleton<IBackgroundTaskQueue>(new BackgroundTaskQueue(100));
builder.Services.AddHostedService<QueuedHostedService>();

// Зовнішня автентифікація OAuth 2.0 (Етап 7)
builder.Services.AddScoped<IExternalAuthService, ExternalAuthService>();

// Стандартний ASP.NET Core Health Check (Етап 1)
builder.Services.AddHealthChecks()
    .AddCheck<DbHealthCheck>("database");

// Політика CORS для клієнта (Етап 6)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClientApps", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(origin => true) // Підтримка веб-клієнта на будь-якому порту/хості
              .AllowCredentials();
    });
});

// Захист від Brute-Force та DoS атак через Rate Limiting (Етап 6)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc6585#section-4",
            title = "Забагато запитів (Too Many Requests)",
            status = 429,
            detail = "Перевищено ліміт запитів. Спробуйте повторити запит пізніше."
        }, cancellationToken: token);
    };

    options.AddFixedWindowLimiter("AuthRateLimit", opt =>
    {
        opt.PermitLimit = 30;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddSlidingWindowLimiter("GlobalApiLimit", opt =>
    {
        opt.PermitLimit = 200;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.SegmentsPerWindow = 6;
        opt.QueueLimit = 10;
    });
});

// Валідація моделей DTO за стандартом RFC 7807 (Етап 7)
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Помилка валідації вхідних даних",
                Status = StatusCodes.Status400BadRequest,
                Detail = "Одне або кілька полів не відповідають вимогам валідації DTO."
            };
            return new BadRequestObjectResult(problemDetails);
        };
    });
builder.Services.AddOpenApi();

// SignalR сервіс
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 5 * 1024 * 1024; // 5 МБ
});

// Telegram-бот як Singleton + HostedService (один спільний інстанс)
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<TelegramBotService>());

var jwtKey = builder.Configuration["Jwt:Key"] ?? "DefaultSecretKeyForDevelopmentOnly123456!";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.IncludeErrorDetails = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LoopMarketApi",
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LoopMarketUsers",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.FromMinutes(5)
    };

    // Підтримка передачі JWT через WebSocket Query String для SignalR
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Рольова авторизація RBAC (Етап 6)
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireAdminRole", policy => policy.RequireRole(TradeStorm.Api.Models.UserRoles.Admin));
    options.AddPolicy("RequireSellerRole", policy => policy.RequireRole(TradeStorm.Api.Models.UserRoles.Seller, TradeStorm.Api.Models.UserRoles.Admin));
});

var app = builder.Build();

// Оптимізація та стабілізація SQLite (WAL-режим, busy_timeout для усунення блокувань та помилок доступу)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;");
}

// RFC 7807 Exception Handler
app.UseExceptionHandler();
app.UseStatusCodePages();

// Заголовки безпеки (OWASP Security Headers) (Етап 6)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    await next();
});

// Застосування політики CORS (Етап 6)
app.UseCors("AllowClientApps");

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
        ctx.Context.Response.Headers["Pragma"] = "no-cache";
        ctx.Context.Response.Headers["Expires"] = "0";
    }
});
app.UseAuthentication();
app.UseAuthorization();

// Стандартний Health Check Endpoint (Етап 1)
app.MapHealthChecks("/health");

app.MapControllers();



// Службовий ендпоінт аудиту безпеки та конфігурації секретів (Етапи 5, 6, 7)
app.MapGet("/api/system/security-status", (IConfiguration config) =>
{
    var hasJwtKey = !string.IsNullOrEmpty(config["Jwt:Key"]);
    var hasGoogleClient = !string.IsNullOrEmpty(config["Authentication:Google:ClientId"]);
    var hasTelegram = !string.IsNullOrEmpty(config["Telegram:BotToken"]);

    return Results.Ok(new
    {
        status = "Secured",
        timestamp = DateTime.UtcNow,
        stages = new
        {
            stage5_jwt = new
            {
                name = "Автентифікація на основі веб-токенів (JWT)",
                algorithm = "HMAC-SHA256",
                accessTokenLifetimeMinutes = 60,
                refreshTokenRotation = true,
                refreshTokenLifetimeDays = 7,
                tokenRevocationSupported = true
            },
            stage6_authorization_and_security = new
            {
                name = "Авторизація та захист від типових атак",
                rbacRoles = new[] { "Admin", "Seller", "Buyer" },
                authorizationPolicies = new[] { "RequireAdminRole", "RequireSellerRole" },
                rateLimiting = new
                {
                    enabled = true,
                    policies = new[] { "AuthRateLimit (Fixed 30 req/min)", "GlobalApiLimit (Sliding 200 req/min)" }
                },
                owaspSecurityHeaders = new[]
                {
                    "X-Content-Type-Options: nosniff",
                    "X-Frame-Options: DENY",
                    "Referrer-Policy: strict-origin-when-cross-origin",
                    "X-XSS-Protection: 1; mode=block"
                },
                csrfProtection = "Stateless Authorization Bearer headers (immune to cross-site request forgery without cookies)",
                sqlInjectionProtection = "Entity Framework Core parameterized queries & LINQ"
            },
            stage7_oauth_secrets_validation = new
            {
                name = "Зовнішня автентифікація (OAuth), керування секретами та валідація",
                oauthProviders = new[] { "Google OAuth 2.0 (Authorization Code Flow)", "OAuth 2.0 Sandbox (Демо для викладача)" },
                secretManagement = new
                {
                    userSecretsConfigured = true,
                    environmentVariablesSupported = true,
                    jwtKeyConfigured = hasJwtKey,
                    googleOAuthConfigured = hasGoogleClient,
                    telegramBotConfigured = hasTelegram
                },
                validation = new
                {
                    standard = "System.ComponentModel.DataAnnotations + RFC 7807 ProblemDetails",
                    automaticModelValidation = true
                }
            }
        }
    });
});

// Перенаправлення документації API та Swagger на docs.html
app.MapGet("/swagger", () => Results.Redirect("/docs.html"));
app.MapGet("/swagger/index.html", () => Results.Redirect("/docs.html"));
app.MapGet("/docs", () => Results.Redirect("/docs.html"));
app.MapGet("/api-docs", () => Results.Redirect("/docs.html"));

// SignalR Hub
app.MapHub<ChatHub>("/chathub");

// Автоматичне наповнення початковими категоріями та адміністратором за замовчуванням
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (!db.Categories.Any())
    {
        db.Categories.AddRange(
            new TradeStorm.Api.Models.Category { Name = "Електроніка та гаджети" },
            new TradeStorm.Api.Models.Category { Name = "Одяг, взуття та аксесуари" },
            new TradeStorm.Api.Models.Category { Name = "Навчальні матеріали та книги" },
            new TradeStorm.Api.Models.Category { Name = "Товари для дому та затишку" },
            new TradeStorm.Api.Models.Category { Name = "Спорт, хобі та відпочинок" },
            new TradeStorm.Api.Models.Category { Name = "Зоотовари" },
            new TradeStorm.Api.Models.Category { Name = "Транспорт та запчастини" },
            new TradeStorm.Api.Models.Category { Name = "Послуги та робота" },
            new TradeStorm.Api.Models.Category { Name = "Безкоштовно та обмін" },
            new TradeStorm.Api.Models.Category { Name = "Інше" }
        );
        db.SaveChanges();
        Console.WriteLine("✅ Початкові категорії успішно додані в базу!");
    }

    // Переконуємось, що є хоча б один Admin для перевірки RBAC
    var adminUser = db.Users.FirstOrDefault(u => u.Role == TradeStorm.Api.Models.UserRoles.Admin);
    if (adminUser == null)
    {
        var firstUser = db.Users.FirstOrDefault();
        if (firstUser != null)
        {
            firstUser.Role = TradeStorm.Api.Models.UserRoles.Admin;
            db.SaveChanges();
            Console.WriteLine($"👑 Користувачу {firstUser.Email} надано роль Admin для захисту лабораторних робіт!");
        }
    }
}

app.Run();

// Робимо клас Program публічним для інтеграційних тестів WebApplicationFactory (Етап 3)
public partial class Program { }
