namespace Brandora.Web.Services;

public record AdminAccount(string Email, string Name);

public class AdminAuthService(IConfiguration configuration)
{
    private const int AccountCount = 4;

    public AdminAccount? Validate(string email, string password)
    {
        for (var i = 1; i <= AccountCount; i++)
        {
            var envEmail = configuration[$"ADMIN_{i}_EMAIL"];
            var envPassword = configuration[$"ADMIN_{i}_PASSWORD"];
            var envName = configuration[$"ADMIN_{i}_NAME"];

            if (string.IsNullOrEmpty(envEmail) || string.IsNullOrEmpty(envPassword))
                continue;

            if (string.Equals(envEmail, email, StringComparison.OrdinalIgnoreCase) && envPassword == password)
                return new AdminAccount(envEmail, envName ?? envEmail);
        }

        return null;
    }
}
