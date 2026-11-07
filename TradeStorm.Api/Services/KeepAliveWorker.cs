using Microsoft.EntityFrameworkCore;
using TradeStorm.Api.Data;

namespace TradeStorm.Api.Services;

/// <summary>
/// Фоновий сервіс для підтримки активності сервера на Render та бази даних Neon PostgreSQL.
/// Запобігає "засинанню" безкоштовного тарифу Render (cold start 40s) та зупинці Neon compute.
/// </summary>
public class KeepAliveWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<KeepAliveWorker> _logger;

    public KeepAliveWorker(IServiceScopeFactory scopeFactory, ILogger<KeepAliveWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 [KeepAliveWorker] Heartbeat сервіс запущено для підтримки швидкості сервера.");

        // Чекаємо 45 секунд після запуску сервера перед першим пінгом
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Тримаємо з'єднання з базою даних Neon теплим (SELECT 1)
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    await db.Database.ExecuteSqlRawAsync("SELECT 1", stoppingToken);
                }

                // 2. Пінгуємо власний /health endpoint сервера для запобігання засинання Render
                try
                {
                    var response = await client.GetAsync("https://loopmarket-x0uo.onrender.com/health", stoppingToken);
                    _logger.LogInformation("💓 [KeepAliveWorker] Heartbeat статус: {StatusCode}", response.StatusCode);
                }
                catch (Exception pingEx)
                {
                    _logger.LogDebug("Heartbeat ping info: {Msg}", pingEx.Message);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("⚠️ [KeepAliveWorker] Помилка heartbeat: {Msg}", ex.Message);
            }

            // Інтервал 8 хвилин (Render Free Tier засинає через 15 хвилин, Neon через 5-10 хв)
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(8), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
