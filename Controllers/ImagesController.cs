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
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "Файл не вибрано" });

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest(new { error = "Недопустимий формат файлу. Використовуйте JPG, PNG або WEBP." });

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { error = "Розмір файлу не має перевищувати 5 МБ" });

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
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