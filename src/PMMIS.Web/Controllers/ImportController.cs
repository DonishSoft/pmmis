using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;

namespace PMMIS.Web.Controllers;

/// <summary>
/// Импорт данных из Excel
/// </summary>
[Authorize(Roles = UserRoles.PmuAdmin)]
public class ImportController : Controller
{
    private readonly ApplicationDbContext _context;

    public ImportController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Скачать шаблон Excel для импорта
    /// </summary>
    public IActionResult DownloadTemplate()
    {
        using var workbook = new XLWorkbook();
        
        // Districts sheet
        var wsDistricts = workbook.Worksheets.Add("Районы");
        wsDistricts.Cell(1, 1).Value = "Код";
        wsDistricts.Cell(1, 2).Value = "№ сортировки";
        wsDistricts.Cell(1, 3).Value = "Название (рус)*";
        wsDistricts.Cell(1, 4).Value = "Название (тадж)";
        wsDistricts.Cell(1, 5).Value = "Название (англ)";
        StyleHeaderRow(wsDistricts, 5);
        
        // Jamoats sheet
        var wsJamoats = workbook.Worksheets.Add("Джамоаты");
        wsJamoats.Cell(1, 1).Value = "Код района*";
        wsJamoats.Cell(1, 2).Value = "Код";
        wsJamoats.Cell(1, 3).Value = "№ сортировки";
        wsJamoats.Cell(1, 4).Value = "Название (рус)*";
        wsJamoats.Cell(1, 5).Value = "Название (тадж)";
        wsJamoats.Cell(1, 6).Value = "Название (англ)";
        StyleHeaderRow(wsJamoats, 6);
        
        // Villages sheet
        var wsVillages = workbook.Worksheets.Add("Сёла");
        wsVillages.Cell(1, 1).Value = "Код джамоата*";
        wsVillages.Cell(1, 2).Value = "Зона";
        wsVillages.Cell(1, 3).Value = "№ п/п";
        wsVillages.Cell(1, 4).Value = "№ сортировки";
        wsVillages.Cell(1, 5).Value = "Название (рус)*";
        wsVillages.Cell(1, 6).Value = "Название (тадж)";
        wsVillages.Cell(1, 7).Value = "Название (англ)";
        wsVillages.Cell(1, 8).Value = "Население 2020";
        wsVillages.Cell(1, 9).Value = "Население текущее";
        wsVillages.Cell(1, 10).Value = "Женщин";
        wsVillages.Cell(1, 11).Value = "Домохозяйств 2020";
        wsVillages.Cell(1, 12).Value = "Домохозяйств текущее";
        wsVillages.Cell(1, 13).Value = "В охвате (1/0)";
        StyleHeaderRow(wsVillages, 13);
        
        // Schools sheet
        var wsSchools = workbook.Worksheets.Add("Школы");
        wsSchools.Cell(1, 1).Value = "Название села (рус)*";
        wsSchools.Cell(1, 2).Value = "Тип учреждения";
        wsSchools.Cell(1, 3).Value = "№";
        wsSchools.Cell(1, 4).Value = "№ сортировки";
        wsSchools.Cell(1, 5).Value = "Название";
        wsSchools.Cell(1, 6).Value = "Учащихся всего";
        wsSchools.Cell(1, 7).Value = "Девочек";
        wsSchools.Cell(1, 8).Value = "Учителей всего";
        wsSchools.Cell(1, 9).Value = "Женщин-учителей";
        wsSchools.Cell(1, 10).Value = "Водоснабжение (1/0)";
        wsSchools.Cell(1, 11).Value = "Санитария (1/0)";
        wsSchools.Cell(1, 12).Value = "Примечания";
        StyleHeaderRow(wsSchools, 12);
        
        // HealthFacilities sheet
        var wsHealth = workbook.Worksheets.Add("Медучреждения");
        wsHealth.Cell(1, 1).Value = "Название села (рус)*";
        wsHealth.Cell(1, 2).Value = "Тип учреждения";
        wsHealth.Cell(1, 3).Value = "№ сортировки";
        wsHealth.Cell(1, 4).Value = "Название";
        wsHealth.Cell(1, 5).Value = "Персонал всего";
        wsHealth.Cell(1, 6).Value = "Из них женщин";
        wsHealth.Cell(1, 7).Value = "Пациентов/день";
        wsHealth.Cell(1, 8).Value = "Водоснабжение (1/0)";
        wsHealth.Cell(1, 9).Value = "Санитария (1/0)";
        wsHealth.Cell(1, 10).Value = "Примечания";
        StyleHeaderRow(wsHealth, 10);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "import_template.xlsx");
    }

    private void StyleHeaderRow(IXLWorksheet ws, int colCount)
    {
        var headerRange = ws.Range(1, 1, 1, colCount);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
        ws.Columns().AdjustToContents();
    }

    /// <summary>
    /// Импорт данных из Excel
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Файл не выбран";
            return RedirectToAction(nameof(Index));
        }

        var results = new ImportResults();

        try
        {
            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);

            // Import Districts
            if (workbook.Worksheets.Contains("Районы"))
            {
                await ImportDistricts(workbook.Worksheet("Районы"), results);
            }

            // Import Jamoats
            if (workbook.Worksheets.Contains("Джамоаты"))
            {
                await ImportJamoats(workbook.Worksheet("Джамоаты"), results);
            }

            // Import Villages
            if (workbook.Worksheets.Contains("Сёла"))
            {
                await ImportVillages(workbook.Worksheet("Сёла"), results);
            }

            // Import Schools
            if (workbook.Worksheets.Contains("Школы"))
            {
                await ImportSchools(workbook.Worksheet("Школы"), results);
            }

            // Import HealthFacilities
            if (workbook.Worksheets.Contains("Медучреждения"))
            {
                await ImportHealthFacilities(workbook.Worksheet("Медучреждения"), results);
            }

            TempData["Success"] = $"Импорт завершён: Районов: {results.Districts}, Джамоатов: {results.Jamoats}, Сёл: {results.Villages}, Школ: {results.Schools}, Медучреждений: {results.HealthFacilities}";
            if (results.Errors.Any())
            {
                TempData["Errors"] = results.Errors;
            }
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Ошибка импорта: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ImportDistricts(IXLWorksheet ws, ImportResults results)
    {
        var rows = ws.RowsUsed().Skip(1); // Skip header
        foreach (var row in rows)
        {
            try
            {
                var nameRu = row.Cell(3).GetString().Trim();
                if (string.IsNullOrEmpty(nameRu)) continue;

                // Check if exists
                if (await _context.Districts.AnyAsync(d => d.NameRu == nameRu)) continue;

                var district = new District
                {
                    Code = row.Cell(1).GetString().Trim(),
                    SortOrder = row.Cell(2).TryGetValue<int>(out var so) ? so : 0,
                    NameRu = nameRu,
                    NameTj = row.Cell(4).GetString().Trim(),
                    NameEn = row.Cell(5).GetString().Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _context.Districts.Add(district);
                results.Districts++;
            }
            catch (Exception ex)
            {
                results.Errors.Add($"Районы, строка {row.RowNumber()}: {ex.Message}");
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task ImportJamoats(IXLWorksheet ws, ImportResults results)
    {
        var rows = ws.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            try
            {
                var districtCode = row.Cell(1).GetString().Trim();
                var nameRu = row.Cell(4).GetString().Trim();
                if (string.IsNullOrEmpty(nameRu) || string.IsNullOrEmpty(districtCode)) continue;

                var district = await _context.Districts.FirstOrDefaultAsync(d => d.Code == districtCode || d.NameRu == districtCode);
                if (district == null)
                {
                    results.Errors.Add($"Джамоаты, строка {row.RowNumber()}: Район '{districtCode}' не найден");
                    continue;
                }

                // Check if exists
                if (await _context.Jamoats.AnyAsync(j => j.DistrictId == district.Id && j.NameRu == nameRu)) continue;

                var jamoat = new Jamoat
                {
                    DistrictId = district.Id,
                    Code = row.Cell(2).GetString().Trim(),
                    SortOrder = row.Cell(3).TryGetValue<int>(out var so) ? so : 0,
                    NameRu = nameRu,
                    NameTj = row.Cell(5).GetString().Trim(),
                    NameEn = row.Cell(6).GetString().Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                
                if (string.IsNullOrEmpty(jamoat.Code))
                {
                    var count = await _context.Jamoats.CountAsync(j => j.DistrictId == district.Id);
                    jamoat.Code = $"JAM{(count + 1).ToString("D2")}";
                }
                
                _context.Jamoats.Add(jamoat);
                results.Jamoats++;
            }
            catch (Exception ex)
            {
                results.Errors.Add($"Джамоаты, строка {row.RowNumber()}: {ex.Message}");
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task ImportVillages(IXLWorksheet ws, ImportResults results)
    {
        var rows = ws.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            try
            {
                var jamoatCode = row.Cell(1).GetString().Trim();
                var nameRu = row.Cell(5).GetString().Trim();
                if (string.IsNullOrEmpty(nameRu) || string.IsNullOrEmpty(jamoatCode)) continue;

                var jamoat = await _context.Jamoats.FirstOrDefaultAsync(j => j.Code == jamoatCode || j.NameRu == jamoatCode);
                if (jamoat == null)
                {
                    results.Errors.Add($"Сёла, строка {row.RowNumber()}: Джамоат '{jamoatCode}' не найден");
                    continue;
                }

                // Check if exists
                if (await _context.Villages.AnyAsync(v => v.JamoatId == jamoat.Id && v.NameRu == nameRu)) continue;

                var village = new Village
                {
                    JamoatId = jamoat.Id,
                    Zone = row.Cell(2).GetString().Trim(),
                    Number = row.Cell(3).TryGetValue<int>(out var num) ? num : 0,
                    SortOrder = row.Cell(4).TryGetValue<int>(out var so) ? so : 0,
                    NameRu = nameRu,
                    NameTj = row.Cell(6).GetString().Trim(),
                    NameEn = row.Cell(7).GetString().Trim(),
                    Population2020 = row.Cell(8).TryGetValue<int>(out var p2020) ? p2020 : 0,
                    PopulationCurrent = row.Cell(9).TryGetValue<int>(out var pCur) ? pCur : 0,
                    FemalePopulation = row.Cell(10).TryGetValue<int>(out var fem) ? fem : 0,
                    Households2020 = row.Cell(11).TryGetValue<int>(out var h2020) ? h2020 : 0,
                    HouseholdsCurrent = row.Cell(12).TryGetValue<int>(out var hCur) ? hCur : 0,
                    IsCoveredByProject = row.Cell(13).TryGetValue<int>(out var cov) && cov == 1,
                    CreatedAt = DateTime.UtcNow
                };
                
                if (village.Number == 0)
                {
                    var count = await _context.Villages.CountAsync(v => v.JamoatId == jamoat.Id);
                    village.Number = count + 1;
                }
                
                _context.Villages.Add(village);
                results.Villages++;
            }
            catch (Exception ex)
            {
                results.Errors.Add($"Сёла, строка {row.RowNumber()}: {ex.Message}");
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task ImportSchools(IXLWorksheet ws, ImportResults results)
    {
        var rows = ws.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            try
            {
                var villageName = row.Cell(1).GetString().Trim();
                if (string.IsNullOrEmpty(villageName)) continue;

                var village = await _context.Villages.FirstOrDefaultAsync(v => v.NameRu == villageName);
                if (village == null)
                {
                    results.Errors.Add($"Школы, строка {row.RowNumber()}: Село '{villageName}' не найдено");
                    continue;
                }

                var typeName = row.Cell(2).GetString().Trim();
                int? typeId = null;
                if (!string.IsNullOrEmpty(typeName))
                {
                    var type = await _context.EducationInstitutionTypes.FirstOrDefaultAsync(t => t.Name == typeName);
                    typeId = type?.Id;
                }

                var school = new School
                {
                    VillageId = village.Id,
                    TypeId = typeId,
                    Number = row.Cell(3).TryGetValue<int>(out var num) ? num : 0,
                    SortOrder = row.Cell(4).TryGetValue<int>(out var so) ? so : 0,
                    Name = row.Cell(5).GetString().Trim(),
                    TotalStudents = row.Cell(6).TryGetValue<int>(out var ts) ? ts : 0,
                    FemaleStudents = row.Cell(7).TryGetValue<int>(out var fs) ? fs : 0,
                    TeachersCount = row.Cell(8).TryGetValue<int>(out var tc) ? tc : 0,
                    FemaleTeachersCount = row.Cell(9).TryGetValue<int>(out var ftc) ? ftc : 0,
                    HasWaterSupply = row.Cell(10).TryGetValue<int>(out var w) && w == 1,
                    HasSanitation = row.Cell(11).TryGetValue<int>(out var s) && s == 1,
                    Notes = row.Cell(12).GetString().Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                
                if (school.Number == 0)
                {
                    var count = await _context.Schools.CountAsync(s => s.VillageId == village.Id);
                    school.Number = count + 1;
                }
                
                _context.Schools.Add(school);
                results.Schools++;
            }
            catch (Exception ex)
            {
                results.Errors.Add($"Школы, строка {row.RowNumber()}: {ex.Message}");
            }
        }
        await _context.SaveChangesAsync();
    }

    private async Task ImportHealthFacilities(IXLWorksheet ws, ImportResults results)
    {
        var rows = ws.RowsUsed().Skip(1);
        foreach (var row in rows)
        {
            try
            {
                var villageName = row.Cell(1).GetString().Trim();
                if (string.IsNullOrEmpty(villageName)) continue;

                var village = await _context.Villages.FirstOrDefaultAsync(v => v.NameRu == villageName);
                if (village == null)
                {
                    results.Errors.Add($"Медучреждения, строка {row.RowNumber()}: Село '{villageName}' не найдено");
                    continue;
                }

                var typeName = row.Cell(2).GetString().Trim();
                int? typeId = null;
                if (!string.IsNullOrEmpty(typeName))
                {
                    var type = await _context.HealthFacilityTypes.FirstOrDefaultAsync(t => t.Name == typeName);
                    typeId = type?.Id;
                }

                var facility = new HealthFacility
                {
                    VillageId = village.Id,
                    TypeId = typeId,
                    SortOrder = row.Cell(3).TryGetValue<int>(out var so) ? so : 0,
                    Name = row.Cell(4).GetString().Trim(),
                    TotalStaff = row.Cell(5).TryGetValue<int>(out var ts) ? ts : 0,
                    FemaleStaff = row.Cell(6).TryGetValue<int>(out var fs) ? fs : 0,
                    PatientsPerDay = row.Cell(7).TryGetValue<int>(out var ppd) ? ppd : 0,
                    HasWaterSupply = row.Cell(8).TryGetValue<int>(out var w) && w == 1,
                    HasSanitation = row.Cell(9).TryGetValue<int>(out var s) && s == 1,
                    Notes = row.Cell(10).GetString().Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.HealthFacilities.Add(facility);
                results.HealthFacilities++;
            }
            catch (Exception ex)
            {
                results.Errors.Add($"Медучреждения, строка {row.RowNumber()}: {ex.Message}");
            }
        }
        await _context.SaveChangesAsync();
    }

    private class ImportResults
    {
        public int Districts { get; set; }
        public int Jamoats { get; set; }
        public int Villages { get; set; }
        public int Schools { get; set; }
        public int HealthFacilities { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
