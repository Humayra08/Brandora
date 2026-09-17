using System.Net;
using System.Net.Mail;

namespace Brandora.Web.Services;

/// <summary>
/// Sends support email over SMTP. Every setting comes from configuration
/// (environment / .env), the same way <see cref="AdminAuthService"/> reads its
/// accounts — nothing about the mailbox is hard-coded.
///
/// Required keys: SMTP_HOST, SMTP_USER, SMTP_PASSWORD, SMTP_FROM.
/// Optional: SMTP_PORT (default 587), SMTP_FROM_NAME, SMTP_SSL (default true).
/// </summary>
public class EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration["SMTP_HOST"]) &&
        !string.IsNullOrWhiteSpace(configuration["SMTP_USER"]) &&
        !string.IsNullOrWhiteSpace(configuration["SMTP_PASSWORD"]) &&
        !string.IsNullOrWhiteSpace(configuration["SMTP_FROM"]);

    /// <summary>
    /// Sends one message. Throws when SMTP is not configured or the send fails,
    /// so callers can leave the submission's status untouched and report the error.
    /// </summary>
    public async Task SendAsync(string toEmail, string subject, string body)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "Email is not configured. Set SMTP_HOST, SMTP_USER, SMTP_PASSWORD and SMTP_FROM in the environment.");
        }

        var host = configuration["SMTP_HOST"]!;
        var port = int.TryParse(configuration["SMTP_PORT"], out var parsed) ? parsed : 587;
        var useSsl = !string.Equals(configuration["SMTP_SSL"], "false", StringComparison.OrdinalIgnoreCase);
        var from = configuration["SMTP_FROM"]!;
        var fromName = configuration["SMTP_FROM_NAME"] ?? "Brandora Support";

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = useSsl,
            Credentials = new NetworkCredential(configuration["SMTP_USER"], configuration["SMTP_PASSWORD"])
        };

        using var message = new MailMessage
        {
            From = new MailAddress(from, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        message.To.Add(toEmail);

        try
        {
            await client.SendMailAsync(message);
            logger.LogInformation("Support email sent to {Recipient}", toEmail);
        }
        catch (Exception ex)
        {
            // Logged here, rethrown so the caller keeps the current status.
            logger.LogError(ex, "Failed to send support email to {Recipient}", toEmail);
            throw;
        }
    }
}
