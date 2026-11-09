using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace TradeStorm.Api.Services;

public static class FileUploadValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".avif", ".heic"
    };

    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp", "image/avif", "image/heic"
    };

    public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    public static (bool IsValid, string? ErrorMessage) ValidateImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return (false, "Файл не вибрано або він порожній");

        if (file.Length > MaxFileSizeBytes)
            return (false, "Розмір файлу не може перевищувати 25 МБ");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            return (false, "Непідтримуваний формат файлу. Дозволені формати: JPG, PNG, WEBP, GIF, BMP.");

        if (!AllowedMimeTypes.Contains(file.ContentType) && !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return (false, "Некоректний MIME-тип файлу. Дозволено лише зображення.");

        // Перевірка магічних байтів (сигнатури файлу) для захисту від шкідливих файлів з підробленим розширенням
        try
        {
            using var stream = file.OpenReadStream();
            var header = new byte[8];
            int read = stream.Read(header, 0, header.Length);
            if (read < 4)
                return (false, "Файл пошкоджено");

            bool isValidHeader = false;
            // JPEG: FF D8 FF
            if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) isValidHeader = true;
            // PNG: 89 50 4E 47
            else if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) isValidHeader = true;
            // GIF: GIF8 (47 49 46 38)
            else if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38) isValidHeader = true;
            // BMP: 42 4D (BM)
            else if (header[0] == 0x42 && header[1] == 0x4D) isValidHeader = true;
            // WEBP: RIFF (52 49 46 46)
            else if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46) isValidHeader = true;
            // AVIF / HEIC: ftyp (індекс 4-7)
            else if (read >= 8 && header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) isValidHeader = true;

            if (!isValidHeader)
                return (false, "Вміст файлу не відповідає дійсному формату зображення (сигнатура не розпізнана).");
        }
        catch
        {
            return (false, "Помилка читання файлу під час перевірки сигнатури.");
        }

        return (true, null);
    }

    private static readonly HashSet<string> AllowedVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm", ".mov", ".mkv", ".m4v"
    };

    private static readonly HashSet<string> AllowedVideoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/webm", "video/quicktime", "video/x-matroska", "video/x-m4v"
    };

    public const long MaxVideoFileSizeBytes = 100 * 1024 * 1024; // 100 MB

    public static (bool IsValid, string? ErrorMessage) ValidateVideo(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return (false, "Файл відео не вибрано або він порожній");

        if (file.Length > MaxVideoFileSizeBytes)
            return (false, "Розмір відео не може перевищувати 100 МБ");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension) || !AllowedVideoExtensions.Contains(extension))
            return (false, "Непідтримуваний формат відео. Дозволені формати: MP4, WebM, MOV, MKV, M4V.");

        if (!AllowedVideoMimeTypes.Contains(file.ContentType) && !file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            return (false, "Некоректний MIME-тип файлу. Дозволено лише відеофайли.");

        try
        {
            using var stream = file.OpenReadStream();
            var header = new byte[12];
            int read = stream.Read(header, 0, header.Length);
            if (read < 4)
                return (false, "Файл відео пошкоджено");

            bool isValidHeader = false;
            // WebM / MKV: 1A 45 DF A3
            if (header[0] == 0x1A && header[1] == 0x45 && header[2] == 0xDF && header[3] == 0xA3)
                isValidHeader = true;
            // MP4 / MOV / M4V: ftyp, moov, mdat, free, wide (typically at offset 4..7)
            else if (read >= 8 && (
                (header[4] == 0x66 && header[5] == 0x74 && header[6] == 0x79 && header[7] == 0x70) || // ftyp
                (header[4] == 0x6D && header[5] == 0x6F && header[6] == 0x6F && header[7] == 0x76) || // moov
                (header[4] == 0x6D && header[5] == 0x64 && header[6] == 0x61 && header[7] == 0x74) || // mdat
                (header[4] == 0x66 && header[5] == 0x72 && header[6] == 0x65 && header[7] == 0x65) || // free
                (header[4] == 0x77 && header[5] == 0x69 && header[6] == 0x64 && header[7] == 0x65)))   // wide
            {
                isValidHeader = true;
            }

            if (!isValidHeader)
                return (false, "Вміст файлу не відповідає дійсному формату відео (сигнатура не розпізнана).");
        }
        catch
        {
            return (false, "Помилка читання файлу під час перевірки сигнатури відео.");
        }

        return (true, null);
    }
}
