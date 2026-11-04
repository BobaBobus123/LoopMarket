using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;

    public ImagesController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(35 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Файл не вибрано" });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".jfif", ".bmp", ".gif", ".avif", ".heic" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { error = "Недопустимий формат файлу. Використовуйте JPG, PNG, WEBP, GIF, JFIF або BMP." });

        if (file.Length > 25 * 1024 * 1024)
            return BadRequest(new { error = "Розмір файлу не має перевищувати 25 МБ" });

        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        var fileUrl = $"/uploads/{uniqueFileName}";
        return Ok(new { url = fileUrl });
    }
}
