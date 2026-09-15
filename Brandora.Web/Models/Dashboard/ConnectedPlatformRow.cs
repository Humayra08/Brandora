namespace Brandora.Web.Models.Dashboard;

public class ConnectedPlatformRow
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string IconGradient { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool Verified { get; set; }
    public int Followers { get; set; }
    public string FollowerLabel { get; set; } = "Followers";
    public bool Connected { get; set; }
}
