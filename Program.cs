using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradeStorm.Api.Data;
using TradeStorm.Api.Hubs;
using TradeStorm.Api.Repositories;
using TradeStorm.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// База даних EF Core
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=loopmarket.db"));

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

builder.Services.AddControllers();
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
        ClockSkew = TimeSpan.Zero
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
            new TradeStorm.Api.Models.Category { Name = "Товари для дому та гуртожитку" },
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
