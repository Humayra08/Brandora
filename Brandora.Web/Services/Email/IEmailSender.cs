namespace Brandora.Web.Services.Email;

public interface IEmailSender
{
    bool IsConfigured { get; }

    // Never throws. Returns true only if the message was actually handed to the SMTP server,
    // so flows that must react to a failed send (e.g. contact replies) can check it, while
    // flows that must not break (registration, approval) can simply ignore the result.
    Task<bool> SendAsync(string toEmail, string toName, string subject, string htmlBody);
}
