using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TradeStorm.Api.Services;

namespace TradeStorm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[EnableRateLimiting("CreateListingLimit")]
public class ImagesController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly TradeStorm.Api.Data.AppDbContext _db;

    public ImagesController(IWebHostEnvironment environment, TradeStorm.Api.Data.AppDbContext db)
    {
        _environment = environment;
        _db = db;
    }

    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        var (isValid, errorMessage) = FileUploadValidator.ValidateImage(file);
        if (!isValid)
        {
            return BadRequest(new { error = errorMessage });
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var webRoot = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(webRoot, "uploads");
        if (!Directory.Exists(uploadsFolder))
            Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var memoryStream = new MemoryStream())
        {
            await file.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            try
            {
                var stored = new TradeStorm.Api.Models.StoredFile
                {
                    FileName = uniqueFileName,
                    ContentType = file.ContentType ?? "image/jpeg",
                    Data = fileBytes,
                    CreatedAt = DateTime.UtcNow
                };
                _db.StoredFiles.Add(stored);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StoredFile] DB persist warning: {ex.Message}");
            }
        }

        var fileUrl = $"/uploads/{uniqueFileName}";
        return Ok(new { url = fileUrl });
    }
}
