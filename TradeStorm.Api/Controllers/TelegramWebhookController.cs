using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    private readonly TelegramBotService _botService;

    public TelegramWebhookController(TelegramBotService botService)
    {
        _botService = botService;
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        if (!_botService.HasBotToken || string.IsNullOrEmpty(_botService.WebhookSecretToken))
        {
            return NotFound();
        }

        var expectedSecret = _botService.WebhookSecretToken;
        if (!Request.Headers.TryGetValue("X-Telegram-Bot-Api-Secret-Token", out var providedSecret))
        {
            return Unauthorized();
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedSecret);
        var providedBytes = Encoding.UTF8.GetBytes(providedSecret.ToString());
        if (expectedBytes.Length != providedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Unauthorized();
        }

        Update? update;
        try
        {
            update = await JsonSerializer.DeserializeAsync<Update>(
                Request.Body,
                JsonBotAPI.Options,
                cancellationToken);
        }
        catch
        {
            return BadRequest();
        }

        if (update != null)
        {
            await _botService.ProcessUpdateAsync(update, cancellationToken);
        }

        return Ok();
    }
}
