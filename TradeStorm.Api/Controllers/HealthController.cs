using Microsoft.AspNetCore.Mvc;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "LoopMarket API is running", version = "1.0" });
    }
}