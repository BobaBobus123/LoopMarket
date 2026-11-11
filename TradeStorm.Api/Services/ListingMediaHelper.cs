using System;
using System.Collections.Generic;
using System.Linq;

namespace TradeStorm.Api.Services;

/// <summary>
/// Validates that active listings include at least one real photo (non-placeholder) or a video presentation.
/// </summary>
public static class ListingMediaHelper
{
    public const string MediaRequiredMessage =
        "Додайте щонайменше 1 фотографію або відеопрезентацію. Оголошення без фото або відео не може бути опубліковане.";

    public static bool IsPlaceholderOrEmpty(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        var lower = url.Trim().ToLowerInvariant();
        return lower.Contains("placeholder") || lower.Contains("no-photo") || lower.Contains("placehold.co");
    }

    public static bool HasValidMedia(IEnumerable<string>? imageUrls, string? videoUrl)
    {
        if (!string.IsNullOrWhiteSpace(videoUrl)) return true;
        if (imageUrls == null) return false;
        return imageUrls.Any(u => !IsPlaceholderOrEmpty(u));
    }

    public static bool HasValidMediaRaw(string? imageUrlString, string? videoUrl)
    {
        if (!string.IsNullOrWhiteSpace(videoUrl)) return true;
        if (string.IsNullOrWhiteSpace(imageUrlString)) return false;
        return imageUrlString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(u => !IsPlaceholderOrEmpty(u));
    }
}
