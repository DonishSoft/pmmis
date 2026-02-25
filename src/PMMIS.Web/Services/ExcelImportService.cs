using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Services;

/// <summary>
/// Импорт объёма работ из Excel через ClosedXML (детерминистический парсинг)
/// </summary>
public interface IExcelImportService
{
    Task<ImportResult> ParseExcelAsync(Stream fileStream, int contractId);
}

public class ExcelImportService : IExcelImportService
{
    private readonly ApplicationDbContext _context;

    public ExcelImportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ImportResult> ParseExcelAsync(Stream fileStream, int contractId)
    {
        var result = new ImportResult { ContractId = contractId, Mode = "ClosedXML" };

        try
        {
            using var workbook = new XLWorkbook(fileStream);
            
            // Find the data sheet (стр 4 or similar)
            var worksheet = FindDataSheet(workbook);
            if (worksheet == null)
            {
                result.Errors.Add("Не найден лист с данными (стр 4). Доступные листы: " + 
                    string.Join(", ", workbook.Worksheets.Select(w => w.Name)));
                return result;
            }

            // Parse work items from the sheet
            var items = ParseWorkItems(worksheet);
            result.Items = items;

            // Compare with existing data
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
            result.Errors.Add($"Ошибка чтения файла: {ex.Message}");
        }

        return result;
    }

    private IXLWorksheet? FindDataSheet(XLWorkbook workbook)
    {
        // Try exact names first
        var names = new[] { "стр 4", "стр4", "Стр 4", "Стр4", "Sheet4", "Лист4" };
        foreach (var name in names)
        {
            if (workbook.TryGetWorksheet(name, out var ws))
                return ws;
        }

        // Try to find any sheet with "стр" and "4" in name
        foreach (var ws in workbook.Worksheets)
        {
            if (ws.Name.Contains("стр", StringComparison.OrdinalIgnoreCase) && ws.Name.Contains("4"))
                return ws;
        }
        
        // Fall back to the sheet with most rows
        return workbook.Worksheets.OrderByDescending(w => w.LastRowUsed()?.RowNumber() ?? 0).FirstOrDefault();
    }

    private List<ImportedWorkItem> ParseWorkItems(IXLWorksheet ws)
    {
        var items = new List<ImportedWorkItem>();
        var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
        string currentCategory = "";
        int sortOrder = 0;

        // Data starts from row 8 (rows 1-7 are headers)
        for (int row = 8; row <= lastRow; row++)
        {
            var cellA = ws.Cell(row, 1).GetString().Trim();
            var cellB = ws.Cell(row, 2).GetString().Trim();

            // Skip empty rows
            if (string.IsNullOrWhiteSpace(cellA) && string.IsNullOrWhiteSpace(cellB))
                continue;

            // Check if this is a category/section header
            if (!IsNumeric(cellA) && !string.IsNullOrWhiteSpace(cellB))
            {
                // This is a section header (e.g. "Раздел 1. Крыша и кровля")
                // or a subtotal row (e.g. "Всего по 1.1 Общий пункт")
                if (!cellB.StartsWith("Всего", StringComparison.OrdinalIgnoreCase) &&
                    !cellB.StartsWith("Итого", StringComparison.OrdinalIgnoreCase))
                {
                    currentCategory = cellB;
                }
                continue;
            }

            // This is a data row (has a number in column A)
            if (IsNumeric(cellA) && !string.IsNullOrWhiteSpace(cellB))
            {
                var item = new ImportedWorkItem
                {
                    ItemNumber = cellA,
                    Name = cellB,
                    Unit = ws.Cell(row, 3).GetString().Trim(),
                    Quantity = GetDecimal(ws.Cell(row, 4)),
                    UnitPrice = GetDecimal(ws.Cell(row, 5)),
                    TotalAmount = GetDecimal(ws.Cell(row, 6)),
                    PreviousQuantity = GetDecimal(ws.Cell(row, 7)),
                    ThisPeriodQuantity = GetDecimal(ws.Cell(row, 8)),
                    CumulativeQuantity = GetDecimal(ws.Cell(row, 9)),
                    PreviousAmount = GetDecimal(ws.Cell(row, 10)),
                    ThisPeriodAmount = GetDecimal(ws.Cell(row, 11)),
                    CumulativeAmount = GetDecimal(ws.Cell(row, 12)),
                    Category = currentCategory,
                    SortOrder = sortOrder++
                };

                items.Add(item);
            }
        }

        return items;
    }

    private void CompareWithExisting(ImportResult result, List<ImportedWorkItem> imported, List<ContractWorkItem> existing)
    {
        var matchedExistingIds = new HashSet<int>();

        foreach (var item in imported)
        {
            // Try to match by ItemNumber + Name (best match)
            var match = existing.FirstOrDefault(e =>
                !matchedExistingIds.Contains(e.Id) &&
                e.ItemNumber == item.ItemNumber &&
                NormalizeName(e.Name) == NormalizeName(item.Name));

            // Fallback: match by Name + Unit
            match ??= existing.FirstOrDefault(e =>
                !matchedExistingIds.Contains(e.Id) &&
                NormalizeName(e.Name) == NormalizeName(item.Name) &&
                NormalizeUnit(e.Unit) == NormalizeUnit(item.Unit));

            // Fallback: fuzzy name match
            match ??= existing.FirstOrDefault(e =>
                !matchedExistingIds.Contains(e.Id) &&
                FuzzyMatch(e.Name, item.Name));

            if (match != null)
            {
                matchedExistingIds.Add(match.Id);
                item.ExistingId = match.Id;

                // Check for differences in base data
                if (match.TargetQuantity != item.Quantity)
                    result.Changes.Add(new ImportDifference
                    {
                        ExistingId = match.Id,
                        Name = item.Name,
                        Field = "Количество",
                        OldValue = match.TargetQuantity.ToString("N2"),
                        NewValue = item.Quantity.ToString("N2")
                    });

                if (match.UnitPrice != item.UnitPrice && item.UnitPrice > 0)
                    result.Changes.Add(new ImportDifference
                    {
                        ExistingId = match.Id,
                        Name = item.Name,
                        Field = "Стоимость ед.",
                        OldValue = match.UnitPrice.ToString("N2"),
                        NewValue = item.UnitPrice.ToString("N2")
                    });

                result.Matched.Add(new MatchedItem
                {
                    ExistingId = match.Id,
                    Name = item.Name,
                    ThisPeriodQuantity = item.ThisPeriodQuantity,
                    ThisPeriodAmount = item.ThisPeriodAmount
                });
            }
            else
            {
                result.NewItems.Add(item);
            }
        }

        // Find missing items (in DB but not in file)
        foreach (var ex in existing)
        {
            if (!matchedExistingIds.Contains(ex.Id))
            {
                result.MissingItems.Add(new MissingItem
                {
                    Id = ex.Id,
                    Name = ex.Name,
                    Unit = ex.Unit,
                    ItemNumber = ex.ItemNumber
                });
            }
        }
    }

    private static bool IsNumeric(string value)
    {
        return !string.IsNullOrWhiteSpace(value) && decimal.TryParse(value.Replace(",", "."), out _);
    }

    private static decimal GetDecimal(IXLCell cell)
    {
        try
        {
            if (cell.IsEmpty()) return 0;
            if (cell.DataType == XLDataType.Number)
                return (decimal)cell.GetDouble();
            var str = cell.GetString().Trim().Replace(",", ".").Replace(" ", "");
            return decimal.TryParse(str, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
        }
        catch { return 0; }
    }

    private static string NormalizeName(string name)
    {
        return name.Trim().ToLowerInvariant()
            .Replace("  ", " ")
            .Replace("ё", "е")
            .Replace("й", "и");
    }

    private static string NormalizeUnit(string unit)
    {
        return unit.Trim().ToLowerInvariant()
            .Replace(".", "")
            .Replace(" ", "");
    }

    private static bool FuzzyMatch(string a, string b)
    {
        var na = NormalizeName(a);
        var nb = NormalizeName(b);
        if (na == nb) return true;
        if (na.Length < 10 || nb.Length < 10) return false;
        // Check if one contains the other (for truncated descriptions)
        return na.Contains(nb) || nb.Contains(na) ||
               na[..Math.Min(40, na.Length)] == nb[..Math.Min(40, nb.Length)];
    }
}
