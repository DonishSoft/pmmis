namespace PMMIS.Web.Services;

/// <summary>
/// Сервис отправки Email
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Отправить email
    /// </summary>
    Task<bool> SendAsync(string to, string subject, string body, bool isHtml = true, CancellationToken ct = default);
    
    /// <summary>
    /// Отправить email нескольким получателям
    /// </summary>
    Task<bool> SendAsync(IEnumerable<string> to, string subject, string body, bool isHtml = true, CancellationToken ct = default);
}
