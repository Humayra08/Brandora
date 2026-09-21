using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace Brandora.Web.Services;

// All Brandora media (campaign photos/videos, profile pictures, proof uploads, message
// attachments) is stored in Cloudinary rather than on local disk — a file saved to
// wwwroot only exists on whichever server instance happened to handle the upload, so it's
// invisible to every other user browsing the same content. Cloudinary gives every upload
// a stable, publicly reachable HTTPS URL that's the same for everyone, on every device.
public class MediaUploadService
{
    private readonly Cloudinary? cloudinary;
    private readonly ILogger<MediaUploadService> logger;

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
    private const long MaxProfilePictureBytes = 5L * 1024 * 1024;

    public MediaUploadService(IConfiguration config, ILogger<MediaUploadService> logger)
    {
        this.logger = logger;

        var cloudName = config["CLOUDINARY_CLOUD_NAME"];
        var apiKey = config["CLOUDINARY_API_KEY"];
        var apiSecret = config["CLOUDINARY_API_SECRET"];

        if (string.IsNullOrEmpty(cloudName) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiSecret))
        {
            logger.LogError("Cloudinary is not configured (CLOUDINARY_CLOUD_NAME / CLOUDINARY_API_KEY / CLOUDINARY_API_SECRET) — uploads will fail until it is.");
            cloudinary = null;
        }
        else
        {
            cloudinary = new Cloudinary(new Account(cloudName, apiKey, apiSecret));
        }
    }

    public async Task<(string? Url, string? Type, string? Error)> SaveMediaAsync(IFormFile file, string folder)
    {
        if (cloudinary is null)
        {
            return (null, null, "Media storage isn't configured right now. Please try again later.");
        }

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

        var cloudFolder = $"brandora/{folder}";
        var typeLabel = isDocument ? "document" : isVideo ? "video" : "image";

        try
        {
            await using var stream = file.OpenReadStream();
            var fileDescription = new FileDescription(file.FileName, stream);

            RawUploadResult result;

            if (isDocument)
            {
                // Raw resources don't get a format auto-appended to their delivery URL the
                // way image/video do, so the extension is embedded in the public id itself
                // — otherwise a submitted PDF's link wouldn't end in ".pdf".
                result = await cloudinary.UploadAsync(new RawUploadParams
                {
                    File = fileDescription,
                    Folder = cloudFolder,
                    PublicId = $"{Guid.NewGuid():N}{ext}",
                    Overwrite = false
                });
            }
            else if (isVideo)
            {
                result = await cloudinary.UploadAsync(new VideoUploadParams
                {
                    File = fileDescription,
                    Folder = cloudFolder,
                    PublicId = Guid.NewGuid().ToString("N"),
                    Overwrite = false
                });
            }
            else
            {
                result = await cloudinary.UploadAsync(new ImageUploadParams
                {
                    File = fileDescription,
                    Folder = cloudFolder,
                    PublicId = Guid.NewGuid().ToString("N"),
                    Overwrite = false
                });
            }

            if (result.Error is not null || result.SecureUrl is null)
            {
                logger.LogError("Cloudinary upload failed: {Error}", result.Error?.Message);
                return (null, null, "The upload failed. Please try again.");
            }

            return (result.SecureUrl.ToString(), typeLabel, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cloudinary upload threw an exception.");
            return (null, null, "The upload failed. Please try again.");
        }
    }

    public async Task<(string? Url, string? Error)> SaveProfilePictureAsync(IFormFile file)
    {
        if (cloudinary is null)
        {
            return (null, "Media storage isn't configured right now. Please try again later.");
        }

        if (!AllowedImageTypes.TryGetValue(file.ContentType, out _))
        {
            return (null, "Upload a JPG, PNG, or WEBP image.");
        }

        if (file.Length > MaxProfilePictureBytes)
        {
            return (null, "Image must be 5MB or smaller.");
        }

        try
        {
            await using var stream = file.OpenReadStream();

            var result = await cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "brandora/profiles",
                PublicId = Guid.NewGuid().ToString("N"),
                Overwrite = false
            });

            if (result.Error is not null || result.SecureUrl is null)
            {
                logger.LogError("Cloudinary profile picture upload failed: {Error}", result.Error?.Message);
                return (null, "The upload failed. Please try again.");
            }

            return (result.SecureUrl.ToString(), null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Cloudinary profile picture upload threw an exception.");
            return (null, "The upload failed. Please try again.");
        }
    }

    // Best-effort cleanup, mirroring the old local-disk version: an orphaned Cloudinary
    // asset left behind on failure is harmless, so every failure here is swallowed rather
    // than surfaced to the caller.
    public void DeleteMedia(string? mediaUrl)
    {
        if (cloudinary is null || string.IsNullOrEmpty(mediaUrl))
        {
            return;
        }

        if (!TryParseCloudinaryAsset(mediaUrl, out var resourceType, out var publicId))
        {
            return;
        }

        try
        {
            cloudinary.Destroy(new DeletionParams(publicId) { ResourceType = resourceType });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cloudinary delete failed for {PublicId} (best-effort, ignored).", publicId);
        }
    }

    // Parses a Cloudinary delivery URL, e.g.
    // https://res.cloudinary.com/<cloud>/image/upload/v169.../brandora/campaigns/<id>.jpg
    // back into its resource type and public id so it can be deleted by reference.
    private static bool TryParseCloudinaryAsset(string url, out ResourceType resourceType, out string publicId)
    {
        resourceType = ResourceType.Image;
        publicId = string.Empty;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/');
        var uploadIndex = Array.IndexOf(segments, "upload");

        // segments: [ cloudName, resourceTypeSegment, "upload", "v169...", ...publicIdParts ]
        if (uploadIndex < 2 || uploadIndex + 1 >= segments.Length)
        {
            return false;
        }

        var resourceTypeSegment = segments[uploadIndex - 1];
        resourceType = resourceTypeSegment switch
        {
            "video" => ResourceType.Video,
            "raw" => ResourceType.Raw,
            _ => ResourceType.Image
        };

        var rest = segments.Skip(uploadIndex + 1).ToArray();

        // Drop the leading version segment ("v1699999999") if present.
        if (rest.Length > 0 && rest[0].Length > 1 && rest[0][0] == 'v' && rest[0].Skip(1).All(char.IsDigit))
        {
            rest = rest.Skip(1).ToArray();
        }

        if (rest.Length == 0)
        {
            return false;
        }

        var joined = string.Join('/', rest);

        // Raw public ids keep their extension (it was embedded at upload time); image and
        // video public ids don't, so strip whatever extension Cloudinary appended for delivery.
        if (resourceType != ResourceType.Raw)
        {
            var lastDot = joined.LastIndexOf('.');
            if (lastDot > 0)
            {
                joined = joined[..lastDot];
            }
        }

        publicId = joined;
        return !string.IsNullOrEmpty(publicId);
    }
}
