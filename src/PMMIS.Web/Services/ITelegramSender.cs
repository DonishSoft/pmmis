namespace PMMIS.Web.Services;

/// <summary>
/// Сервис отправки Telegram сообщений
/// </summary>
public interface ITelegramSender
{
    /// <summary>
    /// Отправить сообщение в Telegram
    /// </summary>
    Task<bool> SendAsync(string chatId, string message, CancellationToken ct = default);
    
    /// <summary>
    /// Проверить доступность бота
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
