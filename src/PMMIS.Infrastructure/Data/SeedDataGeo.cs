using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;

namespace PMMIS.Infrastructure.Data;

/// <summary>
/// Засеивание всех географических данных Хатлонской области:
/// районы, джамоаты, населённые пункты, школы и медучреждения.
/// Данные основаны на проектных зонах WSIP (Balkhi, Dusti, Vakhsh).
/// </summary>
public static class SeedDataGeo
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Skip if our geo data already seeded (check for specific code we create)
        if (await context.Set<Jamoat>().AnyAsync(j => j.Code == "BAL-01"))
            return;

        // Ensure districts exist
        var districts = await context.Districts.OrderBy(d => d.Id).ToListAsync();
        if (districts.Count < 3)
            return; // Districts should be created by SeedData first

        var balkhi = districts.First(d => d.Code == "BALKHI");
        var dusti = districts.First(d => d.Code == "DUSTI");
        var vakhsh = districts.First(d => d.Code == "VAKHSH");

        // Seed education institution types
        if (!await context.Set<EducationInstitutionType>().AnyAsync())
        {
            context.Set<EducationInstitutionType>().AddRange(
                new EducationInstitutionType { Name = "Средняя школа", SortOrder = 1 },
                new EducationInstitutionType { Name = "Начальная школа", SortOrder = 2 },
                new EducationInstitutionType { Name = "Гимназия/Лицей", SortOrder = 3 }
            );
            await context.SaveChangesAsync();
        }
        var schoolType = await context.Set<EducationInstitutionType>().FirstAsync();

        // Seed health facility types
        if (!await context.Set<HealthFacilityType>().AnyAsync())
        {
            context.Set<HealthFacilityType>().AddRange(
                new HealthFacilityType { Name = "Центр здоровья (ЦЗ)", SortOrder = 1 },
                new HealthFacilityType { Name = "Дом здоровья (ДЗ)", SortOrder = 2 },
                new HealthFacilityType { Name = "Фельдшерско-акушерский пункт (ФАП)", SortOrder = 3 }
            );
            await context.SaveChangesAsync();
        }
        var healthTypes = await context.Set<HealthFacilityType>().OrderBy(t => t.SortOrder).ToListAsync();

        // ================================================================
        // БАЛХИНСКИЙ РАЙОН (Balkhi District)
        // ================================================================
        await SeedBalkhiAsync(context, balkhi, schoolType, healthTypes);

        // ================================================================
        // ДУСТИЙСКИЙ РАЙОН (Dusti District)
        // ================================================================
        await SeedDustiAsync(context, dusti, schoolType, healthTypes);

        // ================================================================
        // ВАХШСКИЙ РАЙОН (Vakhsh District)
        // ================================================================
        await SeedVakhshAsync(context, vakhsh, schoolType, healthTypes);
    }

    // ────────────────────────────────────────────────────────────────────
    // БАЛХИНСКИЙ РАЙОН
    // ────────────────────────────────────────────────────────────────────
    private static async Task SeedBalkhiAsync(
        ApplicationDbContext context, District district,
        EducationInstitutionType schoolType, List<HealthFacilityType> healthTypes)
    {
        // === Джамоат Хаёти Нав ===
        var j1 = new Jamoat
        {
            Code = "BAL-01", DistrictId = district.Id, SortOrder = 1,
            NameRu = "Хаёти Нав", NameTj = "Ҳаёти Нав", NameEn = "Hayoti Nav"
        };
        context.Set<Jamoat>().Add(j1);
        await context.SaveChangesAsync();

        var j1Villages = new[]
        {
            CreateVillage(j1.Id, 1, "2D", "Хаёти Нав", "Ҳаёти Нав", "Hayoti Nav", 520, 3120, 580, 3480, 1810, true),
            CreateVillage(j1.Id, 2, "2D", "Ибни Сино", "Ибни Сино", "Ibni Sino", 340, 2040, 380, 2280, 1186, true),
            CreateVillage(j1.Id, 3, "2D", "Гулистон", "Гулистон", "Guliston", 280, 1680, 310, 1860, 967, true),
            CreateVillage(j1.Id, 4, "2D", "Навбахор", "Навбаҳор", "Navbahor", 420, 2520, 470, 2820, 1467, true),
            CreateVillage(j1.Id, 5, "2D", "Зарнисор", "Зарнисор", "Zarnisor", 190, 1140, 210, 1260, 655, false),
            CreateVillage(j1.Id, 6, "3D", "Олимтой", "Олимтой", "Olimtoy", 310, 1860, 345, 2070, 1076, true),
        };
        context.Set<Village>().AddRange(j1Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j1Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Кумсангир ===
        var j2 = new Jamoat
        {
            Code = "BAL-02", DistrictId = district.Id, SortOrder = 2,
            NameRu = "Кумсангир", NameTj = "Кумсангир", NameEn = "Qumsangir"
        };
        context.Set<Jamoat>().Add(j2);
        await context.SaveChangesAsync();

        var j2Villages = new[]
        {
            CreateVillage(j2.Id, 1, "2D", "Кумсангир", "Кумсангир", "Qumsangir", 680, 4080, 760, 4560, 2371, true),
            CreateVillage(j2.Id, 2, "2D", "Сарбоз", "Сарбоз", "Sarboz", 290, 1740, 320, 1920, 998, true),
            CreateVillage(j2.Id, 3, "3D", "Янги Обод", "Янги Обод", "Yangi Obod", 240, 1440, 270, 1620, 843, true),
            CreateVillage(j2.Id, 4, "2D", "Мехнатобод", "Меҳнатобод", "Mehnatobod", 350, 2100, 390, 2340, 1217, false),
            CreateVillage(j2.Id, 5, "3D", "Обшорон", "Обшорон", "Obshoron", 180, 1080, 200, 1200, 624, true),
        };
        context.Set<Village>().AddRange(j2Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j2Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Саноат ===
        var j3 = new Jamoat
        {
            Code = "BAL-03", DistrictId = district.Id, SortOrder = 3,
            NameRu = "Саноат", NameTj = "Саноат", NameEn = "Sanoat"
        };
        context.Set<Jamoat>().Add(j3);
        await context.SaveChangesAsync();

        var j3Villages = new[]
        {
            CreateVillage(j3.Id, 1, "2D", "Саноат", "Саноат", "Sanoat", 460, 2760, 510, 3060, 1591, true),
            CreateVillage(j3.Id, 2, "2D", "Мехргон", "Меҳргон", "Mehrgon", 320, 1920, 355, 2130, 1108, true),
            CreateVillage(j3.Id, 3, "3D", "Дехболо", "Деҳболо", "Dehbolo", 210, 1260, 235, 1410, 733, false),
            CreateVillage(j3.Id, 4, "2D", "Зафаробод", "Зафаробод", "Zafarobod", 380, 2280, 425, 2550, 1326, true),
            CreateVillage(j3.Id, 5, "2D", "Сомониён", "Сомониён", "Somoniyon", 270, 1620, 300, 1800, 936, true),
            CreateVillage(j3.Id, 6, "3D", "Гуллакон", "Гуллакон", "Gullakon", 150, 900, 165, 990, 515, false),
        };
        context.Set<Village>().AddRange(j3Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j3Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Хакими ===
        var j4 = new Jamoat
        {
            Code = "BAL-04", DistrictId = district.Id, SortOrder = 4,
            NameRu = "Хакими", NameTj = "Ҳакимӣ", NameEn = "Hakimi"
        };
        context.Set<Jamoat>().Add(j4);
        await context.SaveChangesAsync();

        var j4Villages = new[]
        {
            CreateVillage(j4.Id, 1, "2D", "Хакими", "Ҳакимӣ", "Hakimi", 410, 2460, 455, 2730, 1420, true),
            CreateVillage(j4.Id, 2, "2D", "Юлдузак", "Юлдузак", "Yulduzak", 260, 1560, 290, 1740, 905, true),
            CreateVillage(j4.Id, 3, "3D", "Сурхоб", "Сурхоб", "Surkhob", 200, 1200, 220, 1320, 686, false),
            CreateVillage(j4.Id, 4, "2D", "Тоҷикистон", "Тоҷикистон", "Tojikiston", 340, 2040, 380, 2280, 1186, true),
            CreateVillage(j4.Id, 5, "2D", "Сарвар", "Сарвар", "Sarvar", 300, 1800, 335, 2010, 1045, true),
        };
        context.Set<Village>().AddRange(j4Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j4Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────
    // ДУСТИЙСКИЙ РАЙОН
    // ────────────────────────────────────────────────────────────────────
    private static async Task SeedDustiAsync(
        ApplicationDbContext context, District district,
        EducationInstitutionType schoolType, List<HealthFacilityType> healthTypes)
    {
        // === Джамоат Навобод ===
        var j1 = new Jamoat
        {
            Code = "DUS-01", DistrictId = district.Id, SortOrder = 1,
            NameRu = "Навобод", NameTj = "Навобод", NameEn = "Navobod"
        };
        context.Set<Jamoat>().Add(j1);
        await context.SaveChangesAsync();

        var j1Villages = new[]
        {
            CreateVillage(j1.Id, 1, "2D", "Навобод", "Навобод", "Navobod", 580, 3480, 645, 3870, 2012, true),
            CreateVillage(j1.Id, 2, "2D", "Чилгази", "Чилгазӣ", "Chilgazi", 340, 2040, 380, 2280, 1186, true),
            CreateVillage(j1.Id, 3, "2D", "Истиқлол", "Истиқлол", "Istiqlol", 420, 2520, 470, 2820, 1467, true),
            CreateVillage(j1.Id, 4, "3D", "Дехконобод", "Деҳқонобод", "Dehqonobod", 250, 1500, 280, 1680, 874, false),
            CreateVillage(j1.Id, 5, "2D", "Шаҳринав", "Шаҳринав", "Shahrinav", 380, 2280, 425, 2550, 1326, true),
            CreateVillage(j1.Id, 6, "2D", "Озодагон", "Озодагон", "Ozodagon", 290, 1740, 320, 1920, 998, true),
        };
        context.Set<Village>().AddRange(j1Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j1Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Сомон ===
        var j2 = new Jamoat
        {
            Code = "DUS-02", DistrictId = district.Id, SortOrder = 2,
            NameRu = "Сомон", NameTj = "Сомон", NameEn = "Somon"
        };
        context.Set<Jamoat>().Add(j2);
        await context.SaveChangesAsync();

        var j2Villages = new[]
        {
            CreateVillage(j2.Id, 1, "2D", "Сомон", "Сомон", "Somon", 490, 2940, 545, 3270, 1700, true),
            CreateVillage(j2.Id, 2, "2D", "Пахтакор", "Пахтакор", "Pakhtakor", 310, 1860, 345, 2070, 1076, true),
            CreateVillage(j2.Id, 3, "3D", "Лаълакон", "Лаълакон", "Lalakon", 220, 1320, 245, 1470, 764, true),
            CreateVillage(j2.Id, 4, "2D", "Бахористон", "Баҳористон", "Bahoriston", 360, 2160, 400, 2400, 1248, true),
            CreateVillage(j2.Id, 5, "2D", "Нуробод", "Нуробод", "Nurobod", 270, 1620, 300, 1800, 936, false),
        };
        context.Set<Village>().AddRange(j2Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j2Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Дӯстӣ ===
        var j3 = new Jamoat
        {
            Code = "DUS-03", DistrictId = district.Id, SortOrder = 3,
            NameRu = "Дӯстӣ", NameTj = "Дӯстӣ", NameEn = "Dusti"
        };
        context.Set<Jamoat>().Add(j3);
        await context.SaveChangesAsync();

        var j3Villages = new[]
        {
            CreateVillage(j3.Id, 1, "2D", "Дӯстӣ", "Дӯстӣ", "Dusti", 720, 4320, 800, 4800, 2496, true),
            CreateVillage(j3.Id, 2, "2D", "Себзор", "Себзор", "Sebzor", 380, 2280, 420, 2520, 1310, true),
            CreateVillage(j3.Id, 3, "2D", "Рохати", "Роҳатӣ", "Rohati", 300, 1800, 335, 2010, 1045, true),
            CreateVillage(j3.Id, 4, "3D", "Кимёгарон", "Кимёгарон", "Kimyogoron", 190, 1140, 210, 1260, 655, false),
            CreateVillage(j3.Id, 5, "2D", "Фирдавсӣ", "Фирдавсӣ", "Firdavsi", 440, 2640, 490, 2940, 1529, true),
            CreateVillage(j3.Id, 6, "2D", "Ватан", "Ватан", "Vatan", 260, 1560, 290, 1740, 905, true),
            CreateVillage(j3.Id, 7, "3D", "Мастчоҳ", "Мастчоҳ", "Mastchoh", 170, 1020, 190, 1140, 593, false),
        };
        context.Set<Village>().AddRange(j3Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j3Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Мехнатобод ===
        var j4 = new Jamoat
        {
            Code = "DUS-04", DistrictId = district.Id, SortOrder = 4,
            NameRu = "Мехнатобод", NameTj = "Меҳнатобод", NameEn = "Mehnatobod"
        };
        context.Set<Jamoat>().Add(j4);
        await context.SaveChangesAsync();

        var j4Villages = new[]
        {
            CreateVillage(j4.Id, 1, "2D", "Мехнатобод", "Меҳнатобод", "Mehnatobod", 530, 3180, 590, 3540, 1841, true),
            CreateVillage(j4.Id, 2, "2D", "Тирамоҳ", "Тирамоҳ", "Tiramoh", 310, 1860, 345, 2070, 1076, true),
            CreateVillage(j4.Id, 3, "2D", "Қаламбур", "Қаламбур", "Qalambur", 280, 1680, 310, 1860, 967, true),
            CreateVillage(j4.Id, 4, "3D", "Гармсер", "Гармсер", "Garmser", 200, 1200, 220, 1320, 686, false),
            CreateVillage(j4.Id, 5, "2D", "Хуросон", "Хуросон", "Khuroson", 360, 2160, 400, 2400, 1248, true),
        };
        context.Set<Village>().AddRange(j4Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j4Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────
    // ВАХШСКИЙ РАЙОН
    // ────────────────────────────────────────────────────────────────────
    private static async Task SeedVakhshAsync(
        ApplicationDbContext context, District district,
        EducationInstitutionType schoolType, List<HealthFacilityType> healthTypes)
    {
        // === Джамоат Вахш ===
        var j1 = new Jamoat
        {
            Code = "VAK-01", DistrictId = district.Id, SortOrder = 1,
            NameRu = "Вахш", NameTj = "Вахш", NameEn = "Vakhsh"
        };
        context.Set<Jamoat>().Add(j1);
        await context.SaveChangesAsync();

        var j1Villages = new[]
        {
            CreateVillage(j1.Id, 1, "2D", "Вахш", "Вахш", "Vakhsh", 750, 4500, 835, 5010, 2605, true),
            CreateVillage(j1.Id, 2, "2D", "Иттифоқ", "Иттифоқ", "Ittifoq", 410, 2460, 455, 2730, 1420, true),
            CreateVillage(j1.Id, 3, "2D", "Гулзор", "Гулзор", "Gulzor", 320, 1920, 355, 2130, 1108, true),
            CreateVillage(j1.Id, 4, "3D", "Дашти Гул", "Дашти Гул", "Dashti Gul", 230, 1380, 255, 1530, 796, false),
            CreateVillage(j1.Id, 5, "2D", "Навдиз", "Навдиз", "Navdiz", 380, 2280, 425, 2550, 1326, true),
            CreateVillage(j1.Id, 6, "2D", "Тоҷикон", "Тоҷикон", "Tojikon", 290, 1740, 320, 1920, 998, true),
        };
        context.Set<Village>().AddRange(j1Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j1Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Бешкаппа ===
        var j2 = new Jamoat
        {
            Code = "VAK-02", DistrictId = district.Id, SortOrder = 2,
            NameRu = "Бешкаппа", NameTj = "Бешкаппа", NameEn = "Beshkappa"
        };
        context.Set<Jamoat>().Add(j2);
        await context.SaveChangesAsync();

        var j2Villages = new[]
        {
            CreateVillage(j2.Id, 1, "2D", "Бешкаппа", "Бешкаппа", "Beshkappa", 560, 3360, 625, 3750, 1950, true),
            CreateVillage(j2.Id, 2, "2D", "Тугалангсой", "Тугалангсой", "Tugalangsoy", 340, 2040, 380, 2280, 1186, true),
            CreateVillage(j2.Id, 3, "3D", "Янгиобод", "Янгиобод", "Yangiobod", 270, 1620, 300, 1800, 936, true),
            CreateVillage(j2.Id, 4, "2D", "Оқсой", "Оқсой", "Oqsoy", 390, 2340, 435, 2610, 1357, true),
            CreateVillage(j2.Id, 5, "2D", "Навбод", "Навбод", "Navbod", 220, 1320, 245, 1470, 764, false),
        };
        context.Set<Village>().AddRange(j2Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j2Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Заргар ===
        var j3 = new Jamoat
        {
            Code = "VAK-03", DistrictId = district.Id, SortOrder = 3,
            NameRu = "Заргар", NameTj = "Заргар", NameEn = "Zargar"
        };
        context.Set<Jamoat>().Add(j3);
        await context.SaveChangesAsync();

        var j3Villages = new[]
        {
            CreateVillage(j3.Id, 1, "2D", "Заргар", "Заргар", "Zargar", 480, 2880, 535, 3210, 1669, true),
            CreateVillage(j3.Id, 2, "2D", "Кишлоки Нав", "Кишлоқи Нав", "Kishloqi Nav", 350, 2100, 390, 2340, 1217, true),
            CreateVillage(j3.Id, 3, "3D", "Сафедоб", "Сафедоб", "Safedob", 200, 1200, 220, 1320, 686, false),
            CreateVillage(j3.Id, 4, "2D", "Навзаргар", "Навзаргар", "Navzargar", 310, 1860, 345, 2070, 1076, true),
            CreateVillage(j3.Id, 5, "2D", "Ширинтеппа", "Ширинтеппа", "Shirinteppa", 420, 2520, 470, 2820, 1467, true),
            CreateVillage(j3.Id, 6, "2D", "Оловхона", "Оловхона", "Olovkhona", 260, 1560, 290, 1740, 905, true),
        };
        context.Set<Village>().AddRange(j3Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j3Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();

        // === Джамоат Обикиик ===
        var j4 = new Jamoat
        {
            Code = "VAK-04", DistrictId = district.Id, SortOrder = 4,
            NameRu = "Обикиик", NameTj = "Обикиик", NameEn = "Obikiik"
        };
        context.Set<Jamoat>().Add(j4);
        await context.SaveChangesAsync();

        var j4Villages = new[]
        {
            CreateVillage(j4.Id, 1, "2D", "Обикиик", "Обикиик", "Obikiik", 620, 3720, 690, 4140, 2153, true),
            CreateVillage(j4.Id, 2, "2D", "Гулбоғ", "Гулбоғ", "Gulbogh", 340, 2040, 380, 2280, 1186, true),
            CreateVillage(j4.Id, 3, "2D", "Дурахш", "Дурахш", "Durakhsh", 280, 1680, 310, 1860, 967, true),
            CreateVillage(j4.Id, 4, "3D", "Гунбатор", "Гунбатор", "Gunbator", 190, 1140, 210, 1260, 655, false),
            CreateVillage(j4.Id, 5, "2D", "Хоразмӣ", "Хоразмӣ", "Khorazmi", 450, 2700, 500, 3000, 1560, true),
        };
        context.Set<Village>().AddRange(j4Villages);
        await context.SaveChangesAsync();
        SeedFacilities(context, j4Villages, schoolType, healthTypes);
        await context.SaveChangesAsync();
    }

    // ────────────────────────────────────────────────────────────────────
    // HELPERS
    // ────────────────────────────────────────────────────────────────────

    private static Village CreateVillage(
        int jamoatId, int number, string zone,
        string nameRu, string nameTj, string nameEn,
        int hh2020, int pop2020,
        int hhCurrent, int popCurrent, int femalePop,
        bool isCovered)
    {
        return new Village
        {
            JamoatId = jamoatId,
            Number = number,
            Zone = zone,
            SortOrder = number,
            NameRu = nameRu,
            NameTj = nameTj,
            NameEn = nameEn,
            Households2020 = hh2020,
            Population2020 = pop2020,
            HouseholdsCurrent = hhCurrent,
            PopulationCurrent = popCurrent,
            FemalePopulation = femalePop,
            IsCoveredByProject = isCovered,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Для каждого села: 1 школа + 1 ФАП/ДЗ.
    /// Более крупные сёла (> 400 домохозяйств) получают дополнительную школу.
    /// </summary>
    private static void SeedFacilities(
        ApplicationDbContext context, Village[] villages,
        EducationInstitutionType schoolType, List<HealthFacilityType> healthTypes)
    {
        var schoolNum = 1;
        foreach (var v in villages)
        {
            // Primary school — students = ~35% of population
            var totalStudents = (int)(v.PopulationCurrent * 0.35);
            var femaleStudents = (int)(totalStudents * 0.48);

            context.Set<School>().Add(new School
            {
                VillageId = v.Id,
                Number = schoolNum++,
                Name = $"Мактаби миёна №{schoolNum - 1} ({v.NameRu})",
                TypeId = schoolType.Id,
                SortOrder = 1,
                TotalStudents = totalStudents,
                FemaleStudents = femaleStudents,
                TeachersCount = Math.Max(8, totalStudents / 25),
                FemaleTeachersCount = (int)(Math.Max(8, totalStudents / 25) * 0.65),
                HasWaterSupply = v.IsCoveredByProject,
                HasSanitation = v.IsCoveredByProject
            });

            // Larger villages get a second school
            if (v.HouseholdsCurrent > 400)
            {
                var extraStudents = (int)(v.PopulationCurrent * 0.15);
                context.Set<School>().Add(new School
                {
                    VillageId = v.Id,
                    Number = schoolNum++,
                    Name = $"Мактаби ибтидоӣ №{schoolNum - 1} ({v.NameRu})",
                    TypeId = schoolType.Id,
                    SortOrder = 2,
                    TotalStudents = extraStudents,
                    FemaleStudents = (int)(extraStudents * 0.48),
                    TeachersCount = Math.Max(4, extraStudents / 20),
                    FemaleTeachersCount = (int)(Math.Max(4, extraStudents / 20) * 0.7),
                    HasWaterSupply = v.IsCoveredByProject,
                    HasSanitation = false
                });
            }

            // Health facility: ФАП for small villages, ДЗ for larger ones
            var hfType = v.PopulationCurrent > 2000 ? healthTypes[1] : healthTypes[2]; // ДЗ or ФАП
            var totalStaff = v.PopulationCurrent > 2000 ? 12 : 5;
            context.Set<HealthFacility>().Add(new HealthFacility
            {
                VillageId = v.Id,
                Name = $"{hfType.Name} «{v.NameRu}»",
                TypeId = hfType.Id,
                SortOrder = 1,
                TotalStaff = totalStaff,
                FemaleStaff = (int)(totalStaff * 0.7),
                PatientsPerDay = v.PopulationCurrent / 100,
                HasWaterSupply = v.IsCoveredByProject,
                HasSanitation = v.IsCoveredByProject
            });
        }
    }
}
