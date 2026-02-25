using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PMMIS.Web.Services;

/// <summary>
/// Импорт объёма работ через Gemini AI API
/// </summary>
public interface IAiImportService
{
    Task<ImportResult> ParseExcelWithAiAsync(Stream fileStream, int contractId);
}

public class AiImportService : IAiImportService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;
    private readonly HttpClient _httpClient;
    private readonly ILogger<AiImportService> _logger;

    public AiImportService(ApplicationDbContext context, IConfiguration config, IHttpClientFactory httpClientFactory, ILogger<AiImportService> logger)
    {
        _context = context;
        _config = config;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.Timeout = TimeSpan.FromMinutes(5); // Gemini needs time for large files
        _logger = logger;
    }

    public async Task<ImportResult> ParseExcelWithAiAsync(Stream fileStream, int contractId)
    {
        var result = new ImportResult { ContractId = contractId, Mode = "Gemini AI" };

        try
        {
            // Step 1: Convert Excel to CSV text
            _logger.LogInformation("=== Gemini AI Import Start === ContractId={ContractId}", contractId);
            var csvText = ConvertExcelToCsv(fileStream);
            if (string.IsNullOrEmpty(csvText))
            {
                result.Errors.Add("Не удалось прочитать данные из Excel файла");
                return result;
            }
            _logger.LogInformation("CSV converted. Length={Len} chars", csvText.Length);

            // Step 2: Send to Gemini API
            var apiKey = await _context.AppSettings
                .Where(s => s.Key == "Gemini:ApiKey")
                .Select(s => s.Value)
                .FirstOrDefaultAsync();
            apiKey ??= _config["Gemini:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey))
            {
                result.Errors.Add("API ключ Gemini не настроен. Перейдите в Настройки → API ключи");
                return result;
            }
            _logger.LogInformation("API key found. Calling Gemini...");

            var items = await CallGeminiApi(csvText, apiKey);
            _logger.LogInformation("Gemini returned {Count} items", items?.Count ?? 0);
            if (items == null || items.Count == 0)
            {
                result.Errors.Add("Gemini не смог распознать данные из файла");
                return result;
            }

            result.Items = items;

            // Step 3: Compare with existing
            var existing = await _context.ContractWorkItems
                .Where(w => w.ContractId == contractId)
                .OrderBy(w => w.SortOrder)
                .ToListAsync();

            if (existing.Count == 0)
            {
                result.IsFirstImport = true;
                result.NewItems = items;
            }
            else
            {
                CompareWithExisting(result, items, existing);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini AI import FAILED for ContractId={ContractId}", contractId);
            result.Errors.Add($"Ошибка AI импорта: {ex.Message}");
        }

        return result;
    }

    private string? ConvertExcelToCsv(Stream fileStream)
    {
        try
        {
            using var workbook = new XLWorkbook(fileStream);
            
            // Find data sheet
            IXLWorksheet? ws = null;
            foreach (var sheet in workbook.Worksheets)
            {
                if (sheet.Name.Contains("стр", StringComparison.OrdinalIgnoreCase) && sheet.Name.Contains("4"))
                { ws = sheet; break; }
            }
            ws ??= workbook.Worksheets.OrderByDescending(w => w.LastRowUsed()?.RowNumber() ?? 0).First();

            var sb = new StringBuilder();
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            var maxCol = Math.Min(ws.LastColumnUsed()?.ColumnNumber() ?? 12, 12);

            // Include header rows (4-7)
            for (int row = 4; row <= Math.Min(lastRow, 7); row++)
            {
                var cells = new List<string>();
                for (int col = 1; col <= maxCol; col++)
                    cells.Add(ws.Cell(row, col).GetString().Replace("\n", " ").Replace("\t", " "));
                sb.AppendLine(string.Join("\t", cells));
            }

            // Data rows  
            for (int row = 8; row <= lastRow; row++)
            {
                var cells = new List<string>();
                bool hasData = false;
                for (int col = 1; col <= maxCol; col++)
                {
                    var val = ws.Cell(row, col).GetString().Replace("\n", " ").Replace("\t", " ");
                    cells.Add(val);
                    if (!string.IsNullOrWhiteSpace(val)) hasData = true;
                }
                if (hasData)
                    sb.AppendLine(string.Join("\t", cells));
            }

            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<ImportedWorkItem>?> CallGeminiApi(string csvText, string apiKey)
    {
        var prompt = @"Ты анализируешь таблицу из Excel файла АВР (Акт Выполненных Работ) строительного проекта.

Данные в формате TSV (tab-separated). Структура колонок:
A = № п/п, B = Описание Позиции, C = Ед. Изм., D = Кол-во, E = Стоимость ед., F = Сумма,
G = Предыдущее (кол-во), H = За этот период (кол-во), I = Всего с начала (кол-во),
J = Предыдущее (сумма), K = За этот период (сумма), L = Всего с начала (сумма)

Правила:
1. Строки с ЧИСЛОМ в колонке A = рабочие позиции (items)
2. Строки БЕЗ числа в A, но с текстом в B = категории/разделы (берём как category)
3. Строки начинающиеся с ""Всего"" или ""Итого"" = subtotals, ПРОПУСКАЕМ
4. Пустые строки ПРОПУСКАЕМ

Верни JSON массив ТОЛЬКО рабочих позиций:
[{""itemNumber"":""1"",""name"":""..."",""unit"":""..."",""quantity"":0,""unitPrice"":0,""totalAmount"":0,""category"":""..."",""prevQty"":0,""thisPeriodQty"":0,""totalQty"":0,""prevAmt"":0,""thisPeriodAmt"":0,""totalAmt"":0}]

ВАЖНО: Верни ТОЛЬКО JSON массив, без markdown, без комментариев. Все числовые значения как числа (не строки).

Вот данные:
" + csvText;

        // Truncate if too long (Gemini has token limits)
        if (prompt.Length > 900000)
            prompt = prompt[..900000];

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.1,
                maxOutputTokens = 65536,
                responseMimeType = "application/json"
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3-flash-preview:generateContent?key={apiKey}";

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Gemini API error {response.StatusCode}: {responseText[..Math.Min(200, responseText.Length)]}");
        }

        // Parse Gemini response
        using var doc = JsonDocument.Parse(responseText);
        var content = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrEmpty(content))
            return null;

        // Clean response (remove possible markdown fences)
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var firstNewline = content.IndexOf('\n');
            if (firstNewline >= 0) content = content[(firstNewline + 1)..];
            if (content.EndsWith("```")) content = content[..^3];
            content = content.Trim();
        }

        // Parse JSON array
        var aiItems = JsonSerializer.Deserialize<List<AiWorkItem>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (aiItems == null) return null;

        int sort = 0;
        return aiItems.Select(ai => new ImportedWorkItem
        {
            ItemNumber = ai.ItemNumber,
            Name = ai.Name ?? "",
            Unit = ai.Unit ?? "",
            Quantity = ai.Quantity,
            UnitPrice = ai.UnitPrice,
            TotalAmount = ai.TotalAmount,
            Category = ai.Category,
            PreviousQuantity = ai.PrevQty,
            ThisPeriodQuantity = ai.ThisPeriodQty,
            CumulativeQuantity = ai.TotalQty,
            PreviousAmount = ai.PrevAmt,
            ThisPeriodAmount = ai.ThisPeriodAmt,
            CumulativeAmount = ai.TotalAmt,
            SortOrder = sort++
        }).ToList();
    }

    private void CompareWithExisting(ImportResult result, List<ImportedWorkItem> imported, List<ContractWorkItem> existing)
    {
        // Reuse same comparison logic from ExcelImportService
        var matchedExistingIds = new HashSet<int>();

        foreach (var item in imported)
        {
            var match = existing.FirstOrDefault(e =>
                !matchedExistingIds.Contains(e.Id) &&
                e.ItemNumber == item.ItemNumber &&
                NormalizeName(e.Name) == NormalizeName(item.Name));

            match ??= existing.FirstOrDefault(e =>
                !matchedExistingIds.Contains(e.Id) &&
                NormalizeName(e.Name) == NormalizeName(item.Name));

            if (match != null)
            {
                matchedExistingIds.Add(match.Id);
                item.ExistingId = match.Id;

                if (match.TargetQuantity != item.Quantity)
                    result.Changes.Add(new ImportDifference
                    {
                        ExistingId = match.Id, Name = item.Name,
                        Field = "Количество",
                        OldValue = match.TargetQuantity.ToString("N2"),
                        NewValue = item.Quantity.ToString("N2")
                    });

                result.Matched.Add(new MatchedItem
                {
                    ExistingId = match.Id, Name = item.Name,
                    ThisPeriodQuantity = item.ThisPeriodQuantity,
                    ThisPeriodAmount = item.ThisPeriodAmount
                });
            }
            else
            {
                result.NewItems.Add(item);
            }
        }

        foreach (var ex in existing)
        {
            if (!matchedExistingIds.Contains(ex.Id))
                result.MissingItems.Add(new MissingItem
                {
                    Id = ex.Id, Name = ex.Name, Unit = ex.Unit, ItemNumber = ex.ItemNumber
                });
        }
    }

    private static string NormalizeName(string name) =>
        name.Trim().ToLowerInvariant().Replace("  ", " ").Replace("ё", "е");

    // DTO for Gemini JSON response
    private class AiWorkItem
    {
        public string? ItemNumber { get; set; }
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Category { get; set; }
        public decimal PrevQty { get; set; }
        public decimal ThisPeriodQty { get; set; }
        public decimal TotalQty { get; set; }
        public decimal PrevAmt { get; set; }
        public decimal ThisPeriodAmt { get; set; }
        public decimal TotalAmt { get; set; }
    }
}
