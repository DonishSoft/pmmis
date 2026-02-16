using System.Text;
using System.Text.Json;

namespace PMMIS.Web.Services;

/// <summary>
/// Реализация отправки сообщений через Telegram Bot API
/// </summary>
public class TelegramSender : ITelegramSender
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramSender> _logger;
    private const string TelegramApiUrl = "https://api.telegram.org/bot";

    public TelegramSender(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramSender> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string chatId, string message, CancellationToken ct = default)
    {
        var telegramConfig = _configuration.GetSection("Notifications:Telegram");
        
        if (!telegramConfig.GetValue<bool>("Enabled"))
        {
            _logger.LogWarning("Telegram sending is disabled in configuration");
            return false;
        }

        var botToken = telegramConfig["BotToken"];
        
        if (string.IsNullOrEmpty(botToken))
        {
            _logger.LogError("Telegram bot token is not configured");
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{TelegramApiUrl}{botToken}/sendMessage";
            
            var payload = new
            {
                chat_id = chatId,
                text = message,
                parse_mode = "Markdown",
                disable_web_page_preview = true
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(url, content, ct);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram message sent successfully to chat {ChatId}", chatId);
                return true;
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Telegram API error: {StatusCode} - {Body}", response.StatusCode, responseBody);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message to chat {ChatId}", chatId);
            return false;
        }
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        var telegramConfig = _configuration.GetSection("Notifications:Telegram");
        
        if (!telegramConfig.GetValue<bool>("Enabled"))
            return false;

        var botToken = telegramConfig["BotToken"];
        
        if (string.IsNullOrEmpty(botToken))
            return false;

        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{TelegramApiUrl}{botToken}/getMe";
            
            var response = await client.GetAsync(url, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
