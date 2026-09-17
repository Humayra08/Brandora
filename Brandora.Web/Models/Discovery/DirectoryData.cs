namespace Brandora.Web.Models.Discovery;

/// <summary>
/// Static page chrome for the two public directory pages: filter options, sort
/// options, stats and card colour palettes.
///
/// The profile cards themselves come from admin-approved database rows, which
/// <c>HomeController</c> loads and places onto the view models returned here.
/// </summary>
public static class DirectoryData
{
    /// <summary>Logo banner backgrounds, picked per brand by profile id.</summary>
    public static readonly string[] BrandLogoBackgrounds =
    {
        "linear-gradient(135deg, #d6206a 0%, #a41b6d 100%)",
        "linear-gradient(135deg, #7b3fd4 0%, #5b2bb0 100%)",
        "linear-gradient(135deg, #e0246c 0%, #c2185b 100%)",
        "linear-gradient(135deg, #1b6fd4 0%, #0d4ea8 100%)",
        "linear-gradient(135deg, #6f9c2f 0%, #4e7a1e 100%)"
    };

    /// <summary>Card cover backgrounds, picked per influencer by profile id.</summary>
    public static readonly string[] InfluencerCoverBackgrounds =
    {
        "linear-gradient(135deg, #0d1b47 0%, #123a7a 100%)",
        "linear-gradient(135deg, #14121c 0%, #2a2140 100%)",
        "linear-gradient(135deg, #7b2ff7 0%, #f107a3 60%, #f9a03f 100%)",
        "linear-gradient(135deg, #0b1020 0%, #16233f 100%)",
        "linear-gradient(135deg, #0f1c3f 0%, #1b3566 100%)"
    };

    public static BrandDirectoryViewModel BuildBrandDirectory() => new()
    {
        Filters = new List<DirectoryFilter>
        {
            new()
            {
                Label = "Industry",
                Name = "industry",
                Options = new List<string>
                {
                    "All Industries", "E-Commerce", "Fintech",
                    "Beauty & Personal Care", "Fashion & Lifestyle"
                }
            },
            new()
            {
                Label = "Campaign Type",
                Name = "campaignType",
                Options = new List<string>
                {
                    "All Types", "Sponsored Post", "Product Review",
                    "Brand Ambassador", "Giveaway"
                }
            },
            new()
            {
                Label = "Location",
                Name = "location",
                Options = new List<string>
                {
                    "All Locations", "Dhaka, Bangladesh",
                    "Chattogram, Bangladesh", "Sylhet, Bangladesh"
                }
            }
        },

        SortOptions = new List<string>
        {
            "Newest First", "Oldest First", "Most Campaigns", "Name (A-Z)"
        },

        Stats = new List<DirectoryStat>
        {
            new() { Value = "2,500+", Label = "Active Brands" },
            new() { Value = "15,000+", Label = "Verified Influencers" },
            new() { Value = "8,000+", Label = "Campaigns Completed" },
            new() { Value = "৳120M+", Label = "Paid to Influencers" }
        }
    };

    public static InfluencerDirectoryViewModel BuildInfluencerDirectory() => new()
    {
        Filters = new List<DirectoryFilter>
        {
            new()
            {
                Label = "Platform",
                Name = "platform",
                Options = new List<string>
                {
                    "All Platforms", "Facebook", "TikTok", "Instagram", "YouTube"
                }
            },
            new()
            {
                Label = "Niche",
                Name = "niche",
                Options = new List<string>
                {
                    "All Niches", "Lifestyle", "Tech", "Fashion",
                    "Entertainment", "Beauty", "Travel"
                }
            },
            new()
            {
                Label = "Location",
                Name = "location",
                Options = new List<string>
                {
                    "All Locations", "Dhaka, Bangladesh",
                    "Chattogram, Bangladesh", "Sylhet, Bangladesh"
                }
            },
            new()
            {
                Label = "Followers",
                Name = "followers",
                Options = new List<string>
                {
                    "Any", "10K - 100K", "100K - 500K", "500K - 1M", "1M+"
                }
            }
        },

        SortOptions = new List<string>
        {
            "Followers: High to Low", "Followers: Low to High",
            "Engagement: High to Low", "Newest First"
        },

        Stats = new List<DirectoryStat>
        {
            new() { Value = "15,000+", Label = "Verified Influencers", Icon = "users" },
            new() { Value = "8,000+", Label = "Active Collaborations", Icon = "handshake" },
            new() { Value = "120M+", Label = "Total Reach", Icon = "chart" },
            new() { Value = "98%", Label = "Satisfaction Rate", Icon = "shield" }
        }
    };
}
