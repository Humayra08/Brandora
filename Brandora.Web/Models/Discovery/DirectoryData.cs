namespace Brandora.Web.Models.Discovery;

/// <summary>
/// Static content source for the two public directory pages.
///
/// The database has no rows for these pages yet, and <c>BrandProfile</c> carries
/// neither a location nor a description column, so the directory content lives
/// here for now. Everything is returned as view models, so replacing this class
/// with a repository or EF query later only changes the controller — the Razor
/// views bind to the same shapes either way.
/// </summary>
public static class DirectoryData
{
    public static BrandDirectoryViewModel BuildBrandDirectory() => new()
    {
        TotalBrandsDisplay = "234+",

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

        Brands = new List<BrandCardViewModel>
        {
            new()
            {
                CompanyName = "Daraz Bangladesh",
                Industry = "E-Commerce",
                Location = "Dhaka, Bangladesh",
                Description = "South Asia's leading online shopping platform.",
                ActiveCampaigns = 12,
                LogoText = "daraz",
                LogoBackground = "linear-gradient(135deg, #d6206a 0%, #a41b6d 100%)"
            },
            new()
            {
                CompanyName = "Evaly",
                Industry = "E-Commerce",
                Location = "Dhaka, Bangladesh",
                Description = "Bangladesh's trusted online shopping destination.",
                ActiveCampaigns = 8,
                LogoText = "evaly",
                LogoBackground = "linear-gradient(135deg, #7b3fd4 0%, #5b2bb0 100%)"
            },
            new()
            {
                CompanyName = "bKash Limited",
                Industry = "Fintech",
                Location = "Dhaka, Bangladesh",
                Description = "Leading mobile financial service provider in Bangladesh.",
                ActiveCampaigns = 15,
                LogoText = "bKash",
                LogoBackground = "linear-gradient(135deg, #e0246c 0%, #c2185b 100%)"
            },
            new()
            {
                CompanyName = "Pickaboo.com",
                Industry = "E-Commerce",
                Location = "Dhaka, Bangladesh",
                Description = "Premium electronics and gadgets store in Bangladesh.",
                ActiveCampaigns = 6,
                LogoText = "Pickaboo",
                LogoBackground = "linear-gradient(135deg, #1b6fd4 0%, #0d4ea8 100%)"
            },
            new()
            {
                CompanyName = "Garnier Bangladesh",
                Industry = "Beauty & Personal Care",
                Location = "Dhaka, Bangladesh",
                Description = "Natural beauty care for healthy skin and hair.",
                ActiveCampaigns = 9,
                LogoText = "GARNIER",
                LogoBackground = "linear-gradient(135deg, #6f9c2f 0%, #4e7a1e 100%)"
            },
            new()
            {
                CompanyName = "Lotto Bangladesh",
                Industry = "Fashion & Lifestyle",
                Location = "Dhaka, Bangladesh",
                Description = "Italian sportswear brand for every lifestyle.",
                ActiveCampaigns = 7,
                LogoText = "lotto",
                LogoBackground = "linear-gradient(135deg, #17181c 0%, #000000 100%)"
            }
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
        TotalInfluencersDisplay = "15,000+",

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

        Influencers = new List<InfluencerCardViewModel>
        {
            new()
            {
                FullName = "Nusrat Jahan",
                Niche = "Lifestyle",
                Location = "Dhaka, Bangladesh",
                Followers = 568_000,
                EngagementRate = 5.2m,
                Verified = true,
                Platform = "Facebook",
                CoverBackground = "linear-gradient(135deg, #0d1b47 0%, #123a7a 100%)"
            },
            new()
            {
                FullName = "Sabbir Rahman",
                Niche = "Tech",
                Location = "Dhaka, Bangladesh",
                Followers = 1_200_000,
                EngagementRate = 7.8m,
                Verified = true,
                Platform = "TikTok",
                CoverBackground = "linear-gradient(135deg, #14121c 0%, #2a2140 100%)"
            },
            new()
            {
                FullName = "Tanjila Islam",
                Niche = "Fashion",
                Location = "Chattogram, Bangladesh",
                Followers = 842_000,
                EngagementRate = 6.1m,
                Verified = true,
                Platform = "Instagram",
                CoverBackground = "linear-gradient(135deg, #7b2ff7 0%, #f107a3 60%, #f9a03f 100%)"
            },
            new()
            {
                FullName = "Rakib Hasan",
                Niche = "Entertainment",
                Location = "Dhaka, Bangladesh",
                Followers = 957_000,
                EngagementRate = 8.0m,
                Verified = true,
                Platform = "TikTok",
                CoverBackground = "linear-gradient(135deg, #0b1020 0%, #16233f 100%)"
            },
            new()
            {
                FullName = "Mehzabin Chowdhury",
                Niche = "Beauty",
                Location = "Dhaka, Bangladesh",
                Followers = 612_000,
                EngagementRate = 5.6m,
                Verified = true,
                Platform = "Facebook",
                CoverBackground = "linear-gradient(135deg, #0f1c3f 0%, #1b3566 100%)"
            },
            new()
            {
                FullName = "Adnan Hridoy",
                Niche = "Travel",
                Location = "Sylhet, Bangladesh",
                Followers = 723_000,
                EngagementRate = 6.3m,
                Verified = true,
                Platform = "Instagram",
                CoverBackground = "linear-gradient(135deg, #3d2a6b 0%, #6b4a9e 100%)"
            }
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
