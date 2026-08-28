namespace Brandora.Web.Models.Account;

public class RegisterPageViewModel
{
    public string InitialView { get; set; } = "select";

    public BrandRegisterViewModel Brand { get; set; } = new();

    public InfluencerRegisterViewModel Influencer { get; set; } = new();
}
