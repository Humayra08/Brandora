namespace Brandora.Web.Models.Influencers;

public static class FollowerFormat
{
    public static string Format(int followers)
    {
        if (followers >= 1_000_000)
        {
            return (followers / 1_000_000d).ToString("0.#") + "M";
        }

        if (followers >= 1_000)
        {
            return (followers / 1_000d).ToString("0.#") + "K";
        }

        return followers.ToString();
    }

    public static string InitialsOf(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return "?";
        }

        return parts.Length == 1
            ? parts[0][..1].ToUpperInvariant()
            : (parts[0][..1] + parts[^1][..1]).ToUpperInvariant();
    }

    // Bangladesh Standard Time is a fixed UTC+6 offset with no daylight saving,
    // so a plain add avoids relying on a host-specific timezone database ID.
    public static DateTime ToBangladeshTime(this DateTime utcDateTime) => utcDateTime.AddHours(6);
}
