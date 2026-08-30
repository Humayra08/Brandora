namespace Brandora.Web.Models.Discovery;

/// <summary>
/// A single labelled dropdown in the directory filter bar.
/// </summary>
public class DirectoryFilter
{
    /// <summary>Small caption rendered above the control (e.g. "Industry").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Query-string key, so the control can be wired to real filtering later.</summary>
    public string Name { get; set; } = string.Empty;

    public List<string> Options { get; set; } = new();

    /// <summary>Currently selected option; defaults to the first option when null.</summary>
    public string? Selected { get; set; }

    public string SelectedOrDefault => Selected ?? Options.FirstOrDefault() ?? string.Empty;
}

/// <summary>
/// One column of the dark statistics panel that closes each directory page.
/// </summary>
public class DirectoryStat
{
    /// <summary>Headline figure, pre-formatted for display (e.g. "15,000+").</summary>
    public string Value { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Icon key rendered as a circular badge. Null renders the plain, icon-less
    /// variant used on the brand directory.
    /// </summary>
    public string? Icon { get; set; }

    public bool HasIcon => !string.IsNullOrWhiteSpace(Icon);
}
