namespace Brandora.Web.Models.Domain;

public class BrandProfile
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string CompanyName { get; set; } = string.Empty;
    public string ContactFullName { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public string Industry { get; set; } = string.Empty;
    public string MonthlyBudget { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }

    // Public social profile links the brand chooses to share (Settings → Social
    // Profiles). Each is optional and shown on the brand's profile, and to creators
    // browsing brands, as that platform's own icon linking straight to the profile.
    public string? FacebookUrl { get; set; }
    public string? InstagramUrl { get; set; }
    public string? TikTokUrl { get; set; }
    public string? YouTubeUrl { get; set; }
    public string? LinkedInUrl { get; set; }
    public string? XUrl { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? AdminNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<ShortlistEntry> ShortlistEntries { get; set; } = new List<ShortlistEntry>();

    // The social links this brand has actually filled in, in display order.
    // A method (not a property) so EF Core never tries to map it.
    public List<(SocialPlatform Platform, string Url)> SocialLinks()
    {
        var links = new List<(SocialPlatform, string)>();
        foreach (var platform in SocialPlatform.All)
        {
            var url = platform.Read(this);
            if (!string.IsNullOrWhiteSpace(url))
            {
                links.Add((platform, url));
            }
        }

        return links;
    }
}

/// <summary>
/// The social networks a brand can list on its profile, with the metadata the
/// views need to draw each one as its real platform icon (Bootstrap Icons glyph +
/// the platform's own brand colour). Also used to label a campaign's live post link.
/// </summary>
public sealed class SocialPlatform
{
    public string Key { get; }
    public string Name { get; }
    public string Icon { get; }            // Bootstrap Icons class, e.g. "bi-instagram"
    public string Background { get; }      // CSS background for the round icon chip
    public string Placeholder { get; }
    public string[] Hosts { get; }
    private readonly string _handleUrl;    // {0} = handle, used when only "@handle" is entered
    private readonly Func<BrandProfile, string?> _get;
    private readonly Action<BrandProfile, string?> _set;

    private SocialPlatform(string key, string name, string icon, string background, string placeholder,
        string[] hosts, string handleUrl, Func<BrandProfile, string?> get, Action<BrandProfile, string?> set)
    {
        Key = key; Name = name; Icon = icon; Background = background; Placeholder = placeholder;
        Hosts = hosts; _handleUrl = handleUrl; _get = get; _set = set;
    }

    public static readonly SocialPlatform Facebook = new("facebook", "Facebook", "bi-facebook", "#1877f2",
        "facebook.com/yourbrand", ["facebook.com", "fb.com", "fb.me"], "https://www.facebook.com/{0}",
        b => b.FacebookUrl, (b, v) => b.FacebookUrl = v);

    public static readonly SocialPlatform Instagram = new("instagram", "Instagram", "bi-instagram",
        "radial-gradient(circle at 30% 107%, #fdf497 0%, #fdf497 5%, #fd5949 45%, #d6249f 60%, #285aeb 90%)",
        "instagram.com/yourbrand", ["instagram.com", "instagr.am"], "https://www.instagram.com/{0}",
        b => b.InstagramUrl, (b, v) => b.InstagramUrl = v);

    public static readonly SocialPlatform TikTok = new("tiktok", "TikTok", "bi-tiktok", "#010101",
        "tiktok.com/@yourbrand", ["tiktok.com"], "https://www.tiktok.com/@{0}",
        b => b.TikTokUrl, (b, v) => b.TikTokUrl = v);

    public static readonly SocialPlatform YouTube = new("youtube", "YouTube", "bi-youtube", "#ff0000",
        "youtube.com/@yourbrand", ["youtube.com", "youtu.be"], "https://www.youtube.com/@{0}",
        b => b.YouTubeUrl, (b, v) => b.YouTubeUrl = v);

    public static readonly SocialPlatform LinkedIn = new("linkedin", "LinkedIn", "bi-linkedin", "#0a66c2",
        "linkedin.com/company/yourbrand", ["linkedin.com", "lnkd.in"], "https://www.linkedin.com/company/{0}",
        b => b.LinkedInUrl, (b, v) => b.LinkedInUrl = v);

    public static readonly SocialPlatform X = new("x", "X (Twitter)", "bi-twitter-x", "#000000",
        "x.com/yourbrand", ["x.com", "twitter.com"], "https://x.com/{0}",
        b => b.XUrl, (b, v) => b.XUrl = v);

    public static readonly IReadOnlyList<SocialPlatform> All = [Facebook, Instagram, TikTok, YouTube, LinkedIn, X];

    public string? Read(BrandProfile brand) => _get(brand);
    public void Write(BrandProfile brand, string? value) => _set(brand, value);

    public bool OwnsHost(string host)
    {
        host = host.ToLowerInvariant();
        return Hosts.Any(h => host == h || host.EndsWith("." + h, StringComparison.Ordinal));
    }

    /// <summary>
    /// Turns what the brand typed ("instagram.com/aurora", "@aurora", a full URL)
    /// into a clean https link for this platform. Returns false with a message when
    /// it isn't a link to this platform, so a wrong icon can never be shown.
    /// </summary>
    public bool TryNormalize(string? input, out string? url, out string? error)
    {
        url = null;
        error = null;
        var value = input?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return true; // empty = remove the link
        }

        if (value.Length > 300)
        {
            error = $"{Name} link is too long.";
            return false;
        }

        // A bare handle: "@aurora", "@aurora.skin", or "aurora" (no dots or slashes)
        var handle = value.TrimStart('@');
        var looksLikeHandle = value.StartsWith('@') || (!value.Contains('.') && !value.Contains('/'));
        if (looksLikeHandle && handle.Length > 0 &&
            handle.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '-'))
        {
            url = string.Format(_handleUrl, handle);
            return true;
        }

        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            value = "https://" + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !OwnsHost(uri.Host))
        {
            error = $"Enter your {Name} link (for example {Placeholder}) or your @handle.";
            return false;
        }

        url = new UriBuilder(uri) { Scheme = Uri.UriSchemeHttps, Port = -1 }.Uri.AbsoluteUri;
        return true;
    }

    /// <summary>The platform a link points to, or null for any other website.</summary>
    public static SocialPlatform? FromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return All.FirstOrDefault(p => p.OwnsHost(uri.Host));
    }
}
