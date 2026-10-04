using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TradeStorm.Api.Data;
using TradeStorm.Api.Services;
using TradeStorm.Api.Hubs; // Пространство имен хаба

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<TelegramBotService>();
builder.Services.AddControllers();

// 🔥 1. ДОБАВИЛИ СЕРВИС SIGNALR И УВЕЛИЧИЛИ ЛИМИТ ДЛЯ ФОТО (до 5 МБ)
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 5 * 1024 * 1024; // 5 Мегабайт
});

// Запускает Telegram-бота в фоне
builder.Services.AddHostedService<TelegramBotService>();

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
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

var app = builder.Build();

// app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 🔥 2. МАППИНГ ДЛЯ SIGNALR ХАБА
app.MapHub<ChatHub>("/chathub");

// --- АВТОМАТИЧЕСКОЕ СОЗДАНИЕ КАТЕГОРИЙ ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (!db.Categories.Any())
    {
        db.Categories.AddRange(
            new TradeStorm.Api.Models.Category { Name = "📚 Навчальні матеріали" },
            new TradeStorm.Api.Models.Category { Name = "💻 Електроніка та гаджети" },
            new TradeStorm.Api.Models.Category { Name = "🏠 Для гуртожитку" },
            new TradeStorm.Api.Models.Category { Name = "👕 Одяг та аксесуари" },
            new TradeStorm.Api.Models.Category { Name = "⚽ Спорт та хобі" },
            new TradeStorm.Api.Models.Category { Name = "🎁 Інше" }
        );
        db.SaveChanges();
        Console.WriteLine("✅ Категорії успішно додані в базу!");
    }
}
// ----------------------------------------

app.Run();