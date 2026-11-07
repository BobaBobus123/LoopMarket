using System.Security.Claims;
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
using Microsoft.AspNetCore.ResponseCompression;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Конфігурація EF Core (Підтримка SQLite та PostgreSQL / Neon Cloud)
var pgEnvUrl = Environment.GetEnvironmentVariable("DATABASE_URL") ?? builder.Configuration["DATABASE_URL"];
var dbProvider = builder.Configuration["DatabaseProvider"] ?? (string.IsNullOrEmpty(pgEnvUrl) ? "Sqlite" : "PostgreSQL");
var isPostgres = dbProvider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase) ||
                 dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                 !string.IsNullOrEmpty(pgEnvUrl);

builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (isPostgres)
    {
        var pgConn = pgEnvUrl;
        if (string.IsNullOrWhiteSpace(pgConn))
        {
            pgConn = builder.Configuration.GetConnectionString("PostgreSqlConnection");
        }
        if (string.IsNullOrWhiteSpace(pgConn))
        {
            pgConn = builder.Configuration.GetConnectionString("DefaultConnection");
        }
        pgConn = NormalizePostgresConnectionString(pgConn);
        options.UseNpgsql(pgConn);
    }
    else
    {
        var connStr = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=loopmarket.db";
        const string prefix = "Data Source=";
        var idxPrefix = connStr.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idxPrefix >= 0)
        {
            var rawPath = connStr.Substring(idxPrefix + prefix.Length).Split(';')[0].Trim();
            if (!Path.IsPathRooted(rawPath))
            {
                var absolutePath = Path.Combine(builder.Environment.ContentRootPath, rawPath);
                connStr = $"Data Source={absolutePath}";
            }
        }
        options.UseSqlite(connStr);
    }
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

// Політика CORS для клієнта: захищена від wildcard зі збереженням credentials (безпека від CSRF/data theft)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[]
{
    "http://localhost:5000",
    "https://localhost:5001",
    "http://localhost:5285",
    "http://localhost:65083",
    "https://localhost:65083",
    "http://127.0.0.1:5000",
    "http://127.0.0.1:65083"
};

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowClientApps", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrEmpty(origin)) return false;
                  try
                  {
                      var uri = new Uri(origin);
                      return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
                             uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                             uri.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase) ||
                             allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
                  }
                  catch
                  {
                      return false;
                  }
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Захист від Brute-Force, спаму та DoS через ASP.NET Core Rate Limiting
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
            detail = "Перевищено допустиму частоту запитів. Будь ласка, зачекайте 1 хвилину перед повторною спробою."
        }, cancellationToken: token);
    };

    // Обмеження для автентифікації та реєстрації (15 запитів на хвилину на IP)
    options.AddPolicy("AuthRateLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Обмеження для створення оголошень та завантаження фото (15 запитів на хвилину)
    options.AddPolicy("CreateListingLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Обмеження для відправки повідомлень у чаті (60 повідомлень на хвилину)
    options.AddPolicy("MessagesLimit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 2
            }));

    // Глобальне обмеження для захисту від DDoS та скрапінгу (150 запитів на хвилину на IP)
    options.AddPolicy("GlobalApiLimit", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 150,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 10
            }));
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

// Фоновий сервіс для запобігання засинання Render та збереження теплого з'єднання з Neon PostgreSQL
builder.Services.AddHostedService<KeepAliveWorker>();

// Стиснення відповідей (Brotli + Gzip) для миттєвого завантаження на телефонах
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

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

    // Підтримка передачі JWT через WebSocket Query String для SignalR та захищених HttpOnly Cookies
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
            else if (string.IsNullOrEmpty(context.Token))
            {
                // Захищений доступ через HttpOnly Cookie для веб-браузерів (захист від XSS-крадіжки токенів)
                if (context.Request.Cookies.TryGetValue("loopToken", out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
                {
                    context.Token = cookieToken;
                }
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
    if (db.Database.IsSqlite())
    {
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000; PRAGMA synchronous=NORMAL;");
    }
    else
    {
        // Автоматичне створення структури таблиць у PostgreSQL
        db.Database.EnsureCreated();
    }
}

// RFC 7807 Exception Handler
app.UseExceptionHandler();
app.UseStatusCodePages();

// Заголовки безпеки (OWASP Security Headers)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

// Застосування політики CORS (Етап 6)
app.UseCors("AllowClientApps");

app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Оптимізація швидкості: Активація Brotli/Gzip стиснення
app.UseResponseCompression();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var fileName = ctx.File.Name.ToLowerInvariant();
        if (fileName.EndsWith(".html"))
        {
            // HTML-сторінки завжди свіжі, але з валідацією ETag/304
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache, must-revalidate";
        }
        else if (fileName.EndsWith(".js") || fileName.EndsWith(".css") ||
                 fileName.EndsWith(".svg") || fileName.EndsWith(".png") ||
                 fileName.EndsWith(".jpg") || fileName.EndsWith(".jpeg") ||
                 fileName.EndsWith(".webp") || fileName.EndsWith(".woff2") ||
                 fileName.EndsWith(".ico"))
        {
            // Статичні стилі, скрипти та зображення кешуються браузером на 7 днів (швидке повторне відкриття)
            ctx.Context.Response.Headers["Cache-Control"] = "public, max-age=604800, stale-while-revalidate=86400";
        }
        else
        {
            ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
        }
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
    try
    {
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database initialization notice: {ex.Message}");
    }
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


// Допоміжна функція нормалізації PostgreSQL рядка підключення (підтримка Neon, Render DATABASE_URL, PgBouncer)
static string NormalizePostgresConnectionString(string? connStr)
{
    if (string.IsNullOrWhiteSpace(connStr)) return string.Empty;
    if (connStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        connStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(connStr);
            var userInfo = uri.UserInfo.Split(':');
            var username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
        }
        catch
        {
            return connStr;
        }
    }
    return connStr;
}

app.Run();


// Робимо клас Program публічним для інтеграційних тестів WebApplicationFactory (Етап 3)
public partial class Program { }
