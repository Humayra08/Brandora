using Microsoft.AspNetCore.Identity;

namespace Brandora.Web.Services;

public record AdminAccount(string Email, string Name);

public class AdminAuthService(IConfiguration configuration)
{
    private const int AccountCount = 4;
    private static readonly PasswordHasher<string> Hasher = new();

    // Used by AdminAccountController's dev-only /HashPassword helper so the user can
    // generate a hash for each ADMIN_n_PASSWORD value themselves and paste it into .env —
    // this service never sees or logs a plaintext password beyond the single login check below.
    public static string HashPassword(string password) => Hasher.HashPassword(string.Empty, password);

    public AdminAccount? Validate(string email, string password)
    {
        for (var i = 1; i <= AccountCount; i++)
        {
            var envEmail = configuration[$"ADMIN_{i}_EMAIL"];
            var envPasswordHash = configuration[$"ADMIN_{i}_PASSWORD"];
            var envName = configuration[$"ADMIN_{i}_NAME"];

            if (string.IsNullOrEmpty(envEmail) || string.IsNullOrEmpty(envPasswordHash))
                continue;

            if (!string.Equals(envEmail, email, StringComparison.OrdinalIgnoreCase))
                continue;

            var result = Hasher.VerifyHashedPassword(string.Empty, envPasswordHash, password);
            if (result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded)
            {
                return new AdminAccount(envEmail, envName ?? envEmail);
            }
        }

        return null;
    }
}
