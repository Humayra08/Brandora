using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Brandora.Web.Services.Email;

// Generic SMTP sender — works with Gmail, Brevo SMTP, SendGrid SMTP, etc., whatever
// SMTP_* values are in .env. If a free host blocks outbound SMTP in Phase 6, a second
// HTTPS-API-based IEmailSender implementation can be added and swapped in via DI without
// touching any of the calling code (AccountController, AdminUserVerificationController).
public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["SMTP_HOST"]) &&
        !string.IsNullOrWhiteSpace(configuration["SMTP_USER"]) &&
        !string.IsNullOrWhiteSpace(configuration["SMTP_PASS"]) &&
        !string.IsNullOrWhiteSpace(configuration["EMAIL_FROM_ADDRESS"] ?? configuration["SMTP_USER"]);

    public async Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var host = configuration["SMTP_HOST"];
        var portRaw = configuration["SMTP_PORT"];
        var user = configuration["SMTP_USER"];
        var pass = configuration["SMTP_PASS"];
        var fromAddress = configuration["EMAIL_FROM_ADDRESS"] ?? user;
        var fromName = configuration["EMAIL_FROM_NAME"] ?? "Brandora";

        if (!IsConfigured)
        {
            // No SMTP configured (e.g. a teammate's machine without .env email values yet) —
            // log instead of crashing the calling flow (registration/approval must not break).
            logger.LogWarning("Email not sent to {ToEmail} (\"{Subject}\") — SMTP_* values missing from .env.", toEmail, subject);
            return false;
        }

        var port = int.TryParse(portRaw, out var parsedPort) ? parsedPort : 587;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromAddress!));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host!, port, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(user!, pass!);
            await client.SendAsync(message);
            return true;
        }
        catch (Exception ex)
        {
            // A transient/misconfigured email provider must never break the user-facing action
            // that triggered it (registration, approval, password reset all still need to
            // succeed on the DB side even if this particular email fails to send).
            logger.LogError(ex, "Failed to send email to {ToEmail} (\"{Subject}\").", toEmail, subject);
            return false;
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }
}
