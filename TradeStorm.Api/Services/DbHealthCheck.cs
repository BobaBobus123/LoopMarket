using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TradeStorm.Api.Data;

namespace TradeStorm.Api.Services;

public class DbHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DbHealthCheck(AppDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            bool canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("База даних SQLite доступна та активна")
                : HealthCheckResult.Unhealthy("Неможливо з'єднатися з базою даних SQLite");
        }
        catch (System.Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Збій з'єднання з базою даних: {ex.Message}");
        }
    }
}
