using System.Security.Cryptography;
using System.Text;
using Brandora.Web.Data;
using Brandora.Web.Models.Account;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    IEmailSender emailSender) : Controller
{
    private const int CodeExpiryMinutes = 15;
    private const int MaxCodeAttempts = 5;

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }

    // Generates a 6-digit code, stores its hash, and returns the raw code so the caller
    // can hand it to the (not-yet-built, Phase 5) email service. Until that exists, the
    // raw code is shown on-screen in Development only — never logged or emailed in plaintext.
    private async Task<string> IssueEmailVerificationCodeAsync(ApplicationUser user)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        db.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            UserId = user.Id,
            CodeHash = HashCode(code),
            Attempts = 0,
            Used = false,
            ExpiresAt = DateTime.UtcNow.AddMinutes(CodeExpiryMinutes)
        });
        await db.SaveChangesAsync();

        var firstName = user.DisplayName.Split(' ')[0];
        var (subject, html) = EmailTemplates.VerificationCode(firstName, code);
        await emailSender.SendAsync(user.Email!, user.DisplayName, subject, html);

        return code;
    }

    public IActionResult Register(string? role)
    {
        var initialView = string.Equals(role, "Brand", StringComparison.OrdinalIgnoreCase)
            ? "brand"
            : string.Equals(role, "Influencer", StringComparison.OrdinalIgnoreCase)
                ? "influencer"
                : "select";

        return View(new RegisterPageViewModel { InitialView = initialView });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterBrand(BrandRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Register", new RegisterPageViewModel { InitialView = "brand", Brand = model });
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.CompanyName,
            Role = UserRole.Brand
        };

        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Register", new RegisterPageViewModel { InitialView = "brand", Brand = model });
        }

        db.BrandProfiles.Add(new BrandProfile
        {
            UserId = user.Id,
            CompanyName = model.CompanyName,
            ContactFullName = model.ContactFullName,
            WebsiteUrl = model.WebsiteUrl,
            Industry = model.Industry,
            MonthlyBudget = model.MonthlyBudget
        });
        await db.SaveChangesAsync();

        await IssueEmailVerificationCodeAsync(user);

        return RedirectToAction("VerifyEmail", new { email = model.Email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterInfluencer(InfluencerRegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Register", new RegisterPageViewModel { InitialView = "influencer", Influencer = model });
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            DisplayName = model.FullName,
            Role = UserRole.Influencer
        };

        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View("Register", new RegisterPageViewModel { InitialView = "influencer", Influencer = model });
        }

        db.InfluencerProfiles.Add(new InfluencerProfile
        {
            UserId = user.Id,
            FullName = model.FullName,
            PrimaryPlatform = model.PrimaryPlatform,
            PlatformUsername = model.PlatformUsername,
            ContentNiche = model.ContentNiche,
            AudienceSize = model.AudienceSize,
            Followers = model.Followers,
            EngagementRate = model.EngagementRate,
            Location = model.Location,
            Bio = model.Bio,
            RateNote = model.RateNote
        });
        await db.SaveChangesAsync();

        await IssueEmailVerificationCodeAsync(user);

        return RedirectToAction("VerifyEmail", new { email = model.Email });
    }

    public async Task<IActionResult> VerifyEmail(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return RedirectToAction("Register");
        }

        if (user.EmailConfirmed)
        {
            return RedirectToAction("PendingReview", new { email });
        }

        return View(new VerifyEmailViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "We couldn't find that account.");
            return View(model);
        }

        if (user.EmailConfirmed)
        {
            return RedirectToAction("PendingReview", new { email = model.Email });
        }

        var pendingCode = await db.EmailVerificationCodes
            .Where(c => c.UserId == user.Id && !c.Used)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (pendingCode is null || pendingCode.ExpiresAt < DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "That code has expired. Request a new one below.");
            return View(model);
        }

        if (pendingCode.Attempts >= MaxCodeAttempts)
        {
            ModelState.AddModelError(string.Empty, "Too many incorrect attempts. Request a new code below.");
            return View(model);
        }

        if (!string.Equals(pendingCode.CodeHash, HashCode(model.Code.Trim()), StringComparison.Ordinal))
        {
            pendingCode.Attempts++;
            await db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "Incorrect code. Please try again.");
            return View(model);
        }

        pendingCode.Used = true;
        user.EmailConfirmed = true;
        await userManager.UpdateAsync(user);
        await db.SaveChangesAsync();

        return RedirectToAction("PendingReview", new { email = model.Email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendVerificationCode(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && !user.EmailConfirmed)
        {
            await IssueEmailVerificationCodeAsync(user);
        }

        TempData["ResendMessage"] = "If that account exists, a new code has been sent.";
        return RedirectToAction("VerifyEmail", new { email });
    }

    public async Task<IActionResult> PendingReview(string email)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return RedirectToAction("Register");
        }

        var status = user.Role == UserRole.Brand
            ? (await db.BrandProfiles.FirstOrDefaultAsync(b => b.UserId == user.Id))?.VerificationStatus
            : (await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.UserId == user.Id))?.VerificationStatus;

        ViewData["Status"] = status ?? VerificationStatus.Pending;
        ViewData["EmailConfirmed"] = user.EmailConfirmed;
        return View();
    }

    public IActionResult Login()
    {
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Please try again in a few minutes.");
            return View(model);
        }

        var passwordValid = await userManager.CheckPasswordAsync(user, model.Password);

        if (!passwordValid)
        {
            await userManager.AccessFailedAsync(user);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        if (!user.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty, "Please verify your email first.");
            return View(model);
        }

        var status = user.Role == UserRole.Brand
            ? (await db.BrandProfiles.FirstOrDefaultAsync(b => b.UserId == user.Id))?.VerificationStatus
            : (await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.UserId == user.Id))?.VerificationStatus;

        if (status == VerificationStatus.Pending)
        {
            ModelState.AddModelError(string.Empty, "Your account is awaiting admin approval. We'll email you once it's approved.");
            return View(model);
        }

        await signInManager.SignInAsync(user, isPersistent: model.RememberMe);

        if (status == VerificationStatus.Rejected)
        {
            // Rejected users CAN log in — unlike Pending, there's nothing to wait on. They need
            // account access to fix their profile so an admin can re-review it. Land them on
            // Settings (not the full dashboard) with the reason so they know what to fix.
            var reason = user.Role == UserRole.Brand
                ? (await db.BrandProfiles.FirstOrDefaultAsync(b => b.UserId == user.Id))?.RejectionReason
                : (await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.UserId == user.Id))?.RejectionReason;

            TempData["RejectionReason"] = string.IsNullOrWhiteSpace(reason)
                ? "Your registration wasn't approved. Please update your profile."
                : $"Your registration wasn't approved: {reason}. Please update your profile.";

            return user.Role == UserRole.Brand
                ? RedirectToAction("Index", "Settings")
                : RedirectToAction("Index", "InfluencerSettings");
        }

        return user.Role == UserRole.Brand
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("Index", "InfluencerDashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(string email)
    {
        // Always show the same message whether or not the account exists — avoids leaking
        // which emails are registered (account enumeration).
        TempData["ForgotPasswordMessage"] = "If that email is registered, we've sent a reset code to it.";

        var user = await userManager.FindByEmailAsync(email ?? "");
        if (user is not null)
        {
            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

            db.PasswordResetCodes.Add(new PasswordResetCode
            {
                UserId = user.Id,
                CodeHash = HashCode(code),
                Attempts = 0,
                Used = false,
                ExpiresAt = DateTime.UtcNow.AddMinutes(CodeExpiryMinutes)
            });
            await db.SaveChangesAsync();

            var firstName = user.DisplayName.Split(' ')[0];
            var (subject, html) = EmailTemplates.PasswordReset(firstName, code);
            await emailSender.SendAsync(user.Email!, user.DisplayName, subject, html);

            return RedirectToAction("ResetPassword", new { email = user.Email });
        }

        return RedirectToAction("ForgotPassword");
    }

    public IActionResult ResetPassword(string email)
    {
        return View(new ResetPasswordViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Something went wrong. Please request a new code.");
            return View(model);
        }

        var pendingCode = await db.PasswordResetCodes
            .Where(c => c.UserId == user.Id && !c.Used)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();

        if (pendingCode is null || pendingCode.ExpiresAt < DateTime.UtcNow)
        {
            ModelState.AddModelError(string.Empty, "That code has expired. Request a new one.");
            return View(model);
        }

        if (pendingCode.Attempts >= MaxCodeAttempts)
        {
            ModelState.AddModelError(string.Empty, "Too many incorrect attempts. Request a new code.");
            return View(model);
        }

        if (!string.Equals(pendingCode.CodeHash, HashCode(model.Code.Trim()), StringComparison.Ordinal))
        {
            pendingCode.Attempts++;
            await db.SaveChangesAsync();
            ModelState.AddModelError(string.Empty, "Incorrect code. Please try again.");
            return View(model);
        }

        var removePasswordResult = await userManager.RemovePasswordAsync(user);
        if (!removePasswordResult.Succeeded)
        {
            foreach (var error in removePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        var addPasswordResult = await userManager.AddPasswordAsync(user, model.NewPassword);
        if (!addPasswordResult.Succeeded)
        {
            foreach (var error in addPasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        pendingCode.Used = true;
        await db.SaveChangesAsync();

        // Password change bumps the SecurityStamp — logs out every other device/session.
        await userManager.UpdateSecurityStampAsync(user);

        TempData["VerificationMessage"] = "Your password has been reset. Please log in with your new password.";
        return RedirectToAction("Login");
    }
}
