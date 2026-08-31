using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Brandora.Web.Services;

public class MediaUploadService(IWebHostEnvironment env)
{
    private static readonly Dictionary<string, string> AllowedTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/quicktime"] = ".mov",
        ["application/pdf"] = ".pdf"
    };

    private static readonly Dictionary<string, string> AllowedImageTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    private const long MaxImageBytes = 15L * 1024 * 1024;
    private const long MaxVideoBytes = 80L * 1024 * 1024;
    private const long MaxDocumentBytes = 100L * 1024 * 1024;

    public async Task<(string? Url, string? Type, string? Error)> SaveMediaAsync(IFormFile file, string folder)
    {
        if (!AllowedTypes.TryGetValue(file.ContentType, out var ext))
        {
            return (null, null, "Upload a JPG, PNG, WEBP, GIF image, an MP4, WEBM, MOV video, or a PDF.");
        }

        var isVideo = file.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase);
        var isDocument = file.ContentType == "application/pdf";
        var limit = isDocument ? MaxDocumentBytes : isVideo ? MaxVideoBytes : MaxImageBytes;

        if (file.Length > limit)
        {
            var limitLabel = isDocument ? "100MB" : isVideo ? "80MB" : "15MB";
            var kindLabel = isDocument ? "File" : isVideo ? "Video" : "Image";
            return (null, null, $"{kindLabel} must be {limitLabel} or smaller.");
        }

        var dir = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return ($"/uploads/{folder}/{fileName}", isDocument ? "document" : isVideo ? "video" : "image", null);
    }

    public async Task<(string? Url, string? Error)> SaveProfilePictureAsync(IFormFile file)
    {
        if (!AllowedImageTypes.TryGetValue(file.ContentType, out var ext))
        {
            return (null, "Upload a JPG, PNG, or WEBP image.");
        }

        if (file.Length > MaxProfilePictureBytes)
        {
            return (null, "Image must be 5MB or smaller.");
        }

        var dir = Path.Combine(env.WebRootPath, "uploads", "profiles");
        Directory.CreateDirectory(dir);

        var fileName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return ($"/uploads/profiles/{fileName}", null);
    }

    public void DeleteMedia(string? mediaUrl)
    {
        if (string.IsNullOrEmpty(mediaUrl))
        {
            return;
        }

        var relative = mediaUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(env.WebRootPath, relative);

        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; an orphaned file is harmless.
            }
        }
    }
}
