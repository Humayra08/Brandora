namespace Brandora.Web.Services.Email;

public static class EmailTemplates
{
    private const string SupportEmail = "care.brandora@gmail.com";

    // Logo mark, drawn entirely with table cells + inline CSS so it renders
    // without any external image — matches the wordmark used on the site
    // ("Brand" in navy, "ora" in brand blue) plus a small gradient "B" tile.
    private static string LogoHtml => $"""
        <table role="presentation" cellpadding="0" cellspacing="0"><tr>
          <td style="width:34px;height:34px;border-radius:10px;background:linear-gradient(135deg,#a326ff,#5b4dff,#18a9ff);text-align:center;vertical-align:middle;font-family:Georgia,'Times New Roman',serif;font-weight:700;font-size:18px;color:#ffffff;">B</td>
          <td style="padding-left:9px;font-family:'Segoe UI',Arial,sans-serif;font-weight:800;font-size:20px;">
            <span style="color:#171d3d;">Brand</span><span style="color:#3b6bff;">ora</span>
          </td>
        </tr></table>
        """;

    // Icon badge, drawn with nested table shapes and negative margins instead
    // of an external image (Gmail strips position:absolute, so the checkmark
    // is pulled up over the envelope with a negative top margin instead) —
    // an envelope-with-checkmark approximation of the reference mockup.
    private static string IconBadgeHtml(string glyph, bool envelope = true)
    {
        var inner = envelope
            ? $"""
               <table role="presentation" cellpadding="0" cellspacing="0" style="margin:14px auto 0;"><tr>
                 <td style="width:26px;height:0;line-height:0;font-size:0;border-left:26px solid transparent;border-right:0;border-bottom:18px solid #ffffff;"></td>
                 <td style="width:26px;height:0;line-height:0;font-size:0;border-right:26px solid transparent;border-left:0;border-bottom:18px solid #ffffff;"></td>
               </tr></table>
               <div style="width:52px;height:22px;background:#ffffff;border-radius:0 0 8px 8px;margin:0 auto;"></div>
               <table role="presentation" cellpadding="0" cellspacing="0" style="margin:-16px 0 0 36px;"><tr>
                 <td style="width:28px;height:28px;border-radius:50%;background:linear-gradient(135deg,#9b2cff,#5b4dff);text-align:center;vertical-align:middle;font-family:Arial,sans-serif;font-weight:800;font-size:14px;color:#ffffff;line-height:28px;">{glyph}</td>
               </tr></table>
               """
            : $"""
               <table role="presentation" cellpadding="0" cellspacing="0" style="margin:12px auto 0;"><tr>
                 <td style="width:40px;height:40px;border-radius:50%;background:linear-gradient(135deg,#9b2cff,#5b4dff);text-align:center;vertical-align:middle;font-family:Arial,sans-serif;font-weight:800;font-size:19px;color:#ffffff;line-height:40px;">{glyph}</td>
               </tr></table>
               """;

        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0"><tr><td style="width:72px;height:72px;border-radius:50%;background:radial-gradient(circle at 35% 30%,#efe6ff,#dcebff);text-align:center;vertical-align:top;">
              {inner}
            </td></tr></table>
            """;
    }

    private static string CodeBoxesHtml(string code)
    {
        var cells = string.Join("", code.Select(digit => $"""
            <td style="width:44px;height:52px;background:#ffffff;border:1px solid #dce7ff;border-radius:10px;text-align:center;vertical-align:middle;font-family:'Segoe UI',Arial,sans-serif;font-size:26px;font-weight:800;color:#171d3d;">{digit}</td>
            <td style="width:8px;"></td>
            """));

        return $"""
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:22px auto;background:#f6f7fb;border-radius:12px;padding:14px;"><tr>
              {cells}
            </tr></table>
            """;
    }

    private static string ExpiryNoteHtml(string line1, string line2) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="background:#f6f7fb;border-radius:10px;margin-top:4px;">
          <tr>
            <td style="width:44px;padding:14px 0 14px 14px;vertical-align:top;">
              <div style="width:30px;height:30px;border-radius:50%;background:#ece3ff;text-align:center;line-height:30px;font-size:15px;">&#128340;</div>
            </td>
            <td style="padding:14px 14px 14px 10px;font-family:'Segoe UI',Arial,sans-serif;font-size:13px;color:#4a5170;line-height:1.5;">
              <b style="color:#171d3d;">{line1}</b><br />{line2}
            </td>
          </tr>
        </table>
        """;

    private static string Wrap(
        string eyebrow,
        string title,
        string iconGlyph,
        string bodyHtml,
        string? codeDigits = null,
        string? expiryLine1 = null,
        string? expiryLine2 = null,
        string? highlightHtml = null,
        string? ctaText = null,
        string? ctaUrl = null,
        bool iconEnvelope = true)
    {
        var codeBlock = string.IsNullOrEmpty(codeDigits) ? "" : CodeBoxesHtml(codeDigits);
        var expiryBlock = string.IsNullOrEmpty(expiryLine1) ? "" : $"<tr><td style=\"padding-top:6px;\">{ExpiryNoteHtml(expiryLine1, expiryLine2 ?? "")}</td></tr>";
        var highlightBlock = string.IsNullOrEmpty(highlightHtml)
            ? ""
            : $"<tr><td style=\"padding-top:16px;\"><div style=\"padding:14px 16px;background:#fde8ea;border-radius:10px;color:#a71b34;font-weight:600;font-family:'Segoe UI',Arial,sans-serif;font-size:13.5px;\">{highlightHtml}</div></td></tr>";
        var ctaBlock = string.IsNullOrEmpty(ctaText) || string.IsNullOrEmpty(ctaUrl)
            ? ""
            : $"""
               <tr><td style="padding-top:24px;">
                 <a href="{ctaUrl}" style="display:inline-block;background:linear-gradient(105deg,#a326ff,#236eff,#18a9ff);color:#ffffff;text-decoration:none;font-family:'Segoe UI',Arial,sans-serif;font-size:14px;font-weight:700;padding:13px 30px;border-radius:9px;">{ctaText}</a>
               </td></tr>
               """;

        return $"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="utf-8" />
            </head>
            <body style="margin:0;padding:0;background:linear-gradient(180deg,#f2eeff 0%,#eef2ff 45%,#e9f1ff 100%);font-family:'Segoe UI',Arial,sans-serif;">
              <div style="height:10px;background:linear-gradient(105deg,#a326ff,#236eff,#18a9ff);"></div>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="padding:32px 16px;">
                <tr><td align="center">
                  <table role="presentation" width="100%" style="max-width:580px;">

                    <!-- Header: logo + tagline -->
                    <tr><td style="padding:4px 6px 22px;">
                      <table role="presentation" width="100%"><tr>
                        <td align="left">{LogoHtml}</td>
                        <td align="right" style="font-family:'Segoe UI',Arial,sans-serif;font-size:12px;color:#8189a8;">Creators. Brands. A fairer internet.</td>
                      </tr></table>
                    </td></tr>

                    <!-- Card -->
                    <tr><td style="background:linear-gradient(135deg,#f5eeff 0%,#eef4ff 25%,#ffffff 55%);border-radius:18px;border:1px solid #dce7ff;padding:36px 36px 30px;">

                      <table role="presentation" width="100%"><tr>
                        <td align="left" style="vertical-align:top;">
                          <div style="font-family:'Segoe UI',Arial,sans-serif;font-weight:700;font-size:11px;letter-spacing:1.2px;color:#8b2ff2;text-transform:uppercase;">{eyebrow}</div>
                          <div style="margin-top:6px;font-family:'Segoe UI',Arial,sans-serif;font-weight:800;font-size:26px;color:#171d3d;">{title}</div>
                        </td>
                        <td align="right" style="vertical-align:top; width:80px;">
                          {IconBadgeHtml(iconGlyph, iconEnvelope)}
                        </td>
                      </tr></table>

                      <table role="presentation" width="100%" style="margin-top:18px;"><tr><td style="font-family:'Segoe UI',Arial,sans-serif;font-size:14.5px;line-height:1.6;color:#4a5170;">
                        {bodyHtml}
                      </td></tr></table>

                      {codeBlock}

                      <table role="presentation" width="100%">
                        {expiryBlock}
                        {highlightBlock}
                        {ctaBlock}
                      </table>

                      <div style="margin-top:28px;padding-top:20px;border-top:1px solid #eef1fb;text-align:center;">
                        <div style="font-family:'Segoe UI',Arial,sans-serif;font-weight:700;font-size:13px;color:#171d3d;">Need help?</div>
                        <div style="margin-top:4px;font-family:'Segoe UI',Arial,sans-serif;font-size:12.5px;color:#8189a8;">
                          If you're having trouble, contact us at
                          <a href="mailto:{SupportEmail}" style="color:#6b52d8;font-weight:600;text-decoration:none;">{SupportEmail}</a>
                        </div>
                      </div>

                      <table role="presentation" width="100%" style="margin-top:26px;"><tr>
                        <td align="left" style="font-family:'Segoe UI',Arial,sans-serif;font-size:12.5px;color:#4a5170;">
                          Best regards,<br /><b style="color:#171d3d;">The Brandora Team</b>
                        </td>
                        <td align="right" style="font-family:Georgia,'Times New Roman',serif;font-style:italic;font-size:14px;line-height:1.3;color:#6b52d8;">
                          Real Creators<br />Real Impact
                        </td>
                      </tr></table>

                    </td></tr>

                    <!-- Footer -->
                    <tr><td style="padding:22px 10px 6px;text-align:center;font-family:'Segoe UI',Arial,sans-serif;font-size:11px;color:#8189a8;">
                      © {DateTime.UtcNow.Year} Brandora. All rights reserved.<br />
                      <span style="color:#b7bdd6;">Privacy Policy &middot; Terms of Service &middot; </span><a href="mailto:{SupportEmail}" style="color:#8189a8;">Support</a>
                    </td></tr>

                    <!-- Decorative wave -->
                    <tr><td style="padding-top:26px;">
                      <div style="height:64px;border-radius:50% 50% 0 0 / 100% 100% 0 0;background:linear-gradient(100deg,#d9c6ff,#c6dcff);opacity:.55;"></div>
                    </td></tr>

                  </table>
                </td></tr>
              </table>
              <div style="height:10px;background:linear-gradient(105deg,#18a9ff,#236eff,#a326ff);"></div>
            </body>
            </html>
            """;
    }

    public static (string Subject, string Html) VerificationCode(string firstName, string code) => (
        "Your Brandora verification code",
        Wrap("Account Verification", "Verify your email", "&#10003;",
            $"Hi {firstName},<br /><br />Enter this code to verify your email and continue your Brandora registration.",
            codeDigits: code,
            expiryLine1: "This code expires in 15 minutes",
            expiryLine2: "and can only be used once. If you didn't request this, you can ignore this email."));

    public static (string Subject, string Html) Approved(string firstName, string loginUrl) => (
        "You're approved — welcome to Brandora!",
        Wrap("Account Verification", "You're approved!", "&#10003;",
            $"Hi {firstName},<br /><br />Good news — an admin has reviewed and approved your Brandora account. You can log in now and start collaborating.",
            ctaText: "Log In to Brandora", ctaUrl: loginUrl));

    public static (string Subject, string Html) Rejected(string firstName, string reason, string profileUrl) => (
        "An update on your Brandora registration",
        Wrap("Account Verification", "Registration not approved", "&#10005;",
            $"Hi {firstName},<br /><br />An admin reviewed your Brandora registration and it wasn't approved this time, for this reason:",
            highlightHtml: reason,
            ctaText: "Update My Profile", ctaUrl: profileUrl,
            iconEnvelope: false));

    public static (string Subject, string Html) PasswordReset(string firstName, string code) => (
        "Reset your Brandora password",
        Wrap("Account Security", "Reset your password", "&#128273;",
            $"Hi {firstName},<br /><br />Use this code to reset your Brandora password.",
            codeDigits: code,
            expiryLine1: "This code expires in 15 minutes",
            expiryLine2: "and can only be used once. If you didn't request this, your password is still safe — just ignore this email.",
            iconEnvelope: false));
}
