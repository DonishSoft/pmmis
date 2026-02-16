using System.Net;
using System.Net.Mail;

namespace PMMIS.Web.Services;

/// <summary>
/// Реализация отправки Email через SMTP
/// </summary>
public class EmailSender : IEmailSender
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IConfiguration configuration, ILogger<EmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken ct = default)
    {
        return await SendAsync(new[] { to }, subject, body, isHtml, ct);
    }

    public async Task<bool> SendAsync(IEnumerable<string> to, string subject, string body, bool isHtml = true, CancellationToken ct = default)
    {
        var emailConfig = _configuration.GetSection("Notifications:Email");
        
        if (!emailConfig.GetValue<bool>("Enabled"))
        {
            _logger.LogWarning("Email sending is disabled in configuration");
            return false;
        }

        var smtpServer = emailConfig["SmtpServer"];
        var smtpPort = emailConfig.GetValue<int>("SmtpPort", 587);
        var fromEmail = emailConfig["FromEmail"];
        var fromName = emailConfig["FromName"] ?? "PMMIS";
        var username = emailConfig["Username"];
        var password = emailConfig["Password"];
        var useSsl = emailConfig.GetValue<bool>("UseSsl", true);

        if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(fromEmail))
        {
            _logger.LogError("SMTP configuration is incomplete");
            return false;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            foreach (var recipient in to)
            {
                if (!string.IsNullOrWhiteSpace(recipient))
                {
                    message.To.Add(recipient);
                }
            }

            if (message.To.Count == 0)
            {
                _logger.LogWarning("No valid recipients for email");
                return false;
            }

            using var client = new SmtpClient(smtpServer, smtpPort)
            {
                EnableSsl = useSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                client.Credentials = new NetworkCredential(username, password);
            }

            await client.SendMailAsync(message, ct);
            
            _logger.LogInformation("Email sent successfully to {Recipients}", string.Join(", ", to));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipients}", string.Join(", ", to));
            return false;
        }
    }
}
