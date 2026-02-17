using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;

namespace PMMIS.Infrastructure.Data;

public static class SeedData
{
    public static async Task InitializeAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        // Create roles
        var roles = new[]
        {
            new ApplicationRole { Name = UserRoles.PmuAdmin, Description = "Администратор PMU", DescriptionTj = "Маъмури PMU", DescriptionEn = "PMU Administrator" },
            new ApplicationRole { Name = UserRoles.PmuStaff, Description = "Сотрудник PMU", DescriptionTj = "Кормандони PMU", DescriptionEn = "PMU Staff" },
            new ApplicationRole { Name = UserRoles.Accountant, Description = "Бухгалтер", DescriptionTj = "Муҳосиб", DescriptionEn = "Accountant" },
            new ApplicationRole { Name = UserRoles.WorldBank, Description = "Всемирный банк", DescriptionTj = "Бонки Ҷаҳонӣ", DescriptionEn = "World Bank" },
            new ApplicationRole { Name = UserRoles.Contractor, Description = "Подрядчик", DescriptionTj = "Пудратчӣ", DescriptionEn = "Contractor" }
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role.Name!))
            {
                await roleManager.CreateAsync(role);
            }
        }

        // Create default admin user
        var adminEmail = "admin@pmmis.tj";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FirstName = "Администратор",
                LastName = "Системы",
                EmailConfirmed = true,
                PreferredLanguage = "ru",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, "Admin123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, UserRoles.PmuAdmin);
            }
        }

        // Create World Bank viewer user
        var wbEmail = "viewer@worldbank.org";
        if (await userManager.FindByEmailAsync(wbEmail) == null)
        {
            var wbUser = new ApplicationUser
            {
                UserName = wbEmail,
                Email = wbEmail,
                FirstName = "World Bank",
                LastName = "Viewer",
                EmailConfirmed = true,
                PreferredLanguage = "en",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(wbUser, "WBView123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(wbUser, UserRoles.WorldBank);
            }
        }

        // Create Director user (PMU_ADMIN)
        var directorEmail = "director@pmmis.tj";
        if (await userManager.FindByEmailAsync(directorEmail) == null)
        {
            var director = new ApplicationUser
            {
                UserName = directorEmail,
                Email = directorEmail,
                FirstName = "Шарифзода",
                LastName = "Фаррух",
                MiddleName = "Рустамович",
                EmailConfirmed = true,
                PreferredLanguage = "ru",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var dResult = await userManager.CreateAsync(director, "Test123!");
            if (dResult.Succeeded)
            {
                await userManager.AddToRoleAsync(director, UserRoles.PmuAdmin);
            }
        }

        // Create Project Manager user (PMU_STAFF)
        var pmEmail = "pm@pmmis.tj";
        if (await userManager.FindByEmailAsync(pmEmail) == null)
        {
            var pm = new ApplicationUser
            {
                UserName = pmEmail,
                Email = pmEmail,
                FirstName = "Каримова",
                LastName = "Нигина",
                MiddleName = "Абдуллоевна",
                EmailConfirmed = true,
                PreferredLanguage = "ru",
                IsActive = true,
                Gender = Gender.Female,
                CreatedAt = DateTime.UtcNow
            };

            var pmResult = await userManager.CreateAsync(pm, "Test123!");
            if (pmResult.Succeeded)
            {
                await userManager.AddToRoleAsync(pm, UserRoles.PmuStaff);
            }
        }

        // Create Curator user (PMU_STAFF)
        var curatorEmail = "curator@pmmis.tj";
        if (await userManager.FindByEmailAsync(curatorEmail) == null)
        {
            var curator = new ApplicationUser
            {
                UserName = curatorEmail,
                Email = curatorEmail,
                FirstName = "Рахматов",
                LastName = "Бахтиёр",
                MiddleName = "Саидович",
                EmailConfirmed = true,
                PreferredLanguage = "ru",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var cResult = await userManager.CreateAsync(curator, "Test123!");
            if (cResult.Succeeded)
            {
                await userManager.AddToRoleAsync(curator, UserRoles.PmuStaff);
            }
        }

        // Seed Districts
        if (!await context.Districts.AnyAsync())
        {
            var districts = new[]
            {
                new District { Code = "BALKHI", NameRu = "Балхинский район", NameTj = "Ноҳияи Балхӣ", NameEn = "Balkhi District", CreatedAt = DateTime.UtcNow },
                new District { Code = "DUSTI", NameRu = "Дустийский район", NameTj = "Ноҳияи Дӯстӣ", NameEn = "Dusti District", CreatedAt = DateTime.UtcNow },
                new District { Code = "VAKHSH", NameRu = "Вахшский район", NameTj = "Ноҳияи Вахш", NameEn = "Vakhsh District", CreatedAt = DateTime.UtcNow }
            };

            context.Districts.AddRange(districts);
            await context.SaveChangesAsync();
        }

        // Seed default project WSIP-1
        if (!await context.Projects.AnyAsync())
        {
            var project = new Project
            {
                Code = "WSIP-1",
                NameRu = "Проект инвестирования в водоснабжение и санитарию",
                NameTj = "Лоиҳаи сармоягузорӣ дар таъминоти об ва беҳдошт",
                NameEn = "Water Supply and Sanitation Investment Project",
                TotalBudget = 25_165_739.63m,
                StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EndDate = new DateTime(2027, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                CreatedAt = DateTime.UtcNow,
                Status = ProjectStatus.Active
            };

            context.Projects.Add(project);
            await context.SaveChangesAsync();

            // Add components
            var components = new[]
            {
                new Component { 
                    Number = 1, 
                    ProjectId = project.Id, 
                    NameRu = "Инвестиции в объекты ВКХ Вахшской межрайонной системы", 
                    NameTj = "Сармоягузорӣ ба иншооти КТО-и системаи байниноҳиягии Вахш",
                    NameEn = "Investments in WSS facilities of Vakhsh inter-district system",
                    AllocatedBudget = 23_605_879.63m 
                },
                new Component { 
                    Number = 2, 
                    ProjectId = project.Id, 
                    NameRu = "Создание потенциала и изменение поведения", 
                    NameTj = "Сохтори салоҳият ва тағйири рафтор",
                    NameEn = "Capacity Building and Behavior Change",
                    AllocatedBudget = 1_559_860m 
                },
                new Component { 
                    Number = 3, 
                    ProjectId = project.Id, 
                    NameRu = "Управление проектом и поддержка реализации", 
                    NameTj = "Идоракунии лоиҳа ва дастгирии амалисозӣ",
                    NameEn = "Project Management and Implementation Support",
                    AllocatedBudget = 0m 
                }
            };

            context.Components.AddRange(components);
            await context.SaveChangesAsync();
            
            // === DEMO DATA: SubComponents (for Budget by Components widget) ===
            var subComponents = new[]
            {
                new SubComponent
                {
                    ComponentId = components[0].Id,
                    Code = "1.1",
                    NameRu = "Магистральные водопроводы",
                    NameTj = "Қубурҳои асосии об",
                    NameEn = "Main Water Pipelines",
                    AllocatedBudget = 8_000_000m
                },
                new SubComponent
                {
                    ComponentId = components[0].Id,
                    Code = "1.2",
                    NameRu = "Распределительные сети",
                    NameTj = "Шабакаҳои тақсимот",
                    NameEn = "Distribution Networks",
                    AllocatedBudget = 6_000_000m
                },
                new SubComponent
                {
                    ComponentId = components[0].Id,
                    Code = "1.3",
                    NameRu = "Насосные станции",
                    NameTj = "Истгоҳҳои насос",
                    NameEn = "Pump Stations",
                    AllocatedBudget = 5_000_000m
                },
                new SubComponent
                {
                    ComponentId = components[0].Id,
                    Code = "1.4",
                    NameRu = "Очистные сооружения",
                    NameTj = "Иншооти тозакунӣ",
                    NameEn = "Treatment Facilities",
                    AllocatedBudget = 4_605_879.63m
                },
                new SubComponent
                {
                    ComponentId = components[1].Id,
                    Code = "2.1",
                    NameRu = "Обучение персонала водоканала",
                    NameTj = "Омӯзиши кормандони обу канал",
                    NameEn = "Utility Staff Training",
                    AllocatedBudget = 800_000m
                },
                new SubComponent
                {
                    ComponentId = components[1].Id,
                    Code = "2.2",
                    NameRu = "Кампании по гигиене и санитарии",
                    NameTj = "Маъракаҳои гигиена ва санитария",
                    NameEn = "Hygiene and Sanitation Campaigns",
                    AllocatedBudget = 759_860m
                }
            };
            
            context.SubComponents.AddRange(subComponents);
            await context.SaveChangesAsync();
            
            // === DEMO DATA: Procurement Plans (for Procurement Status widget) ===
            var procurements = new[]
            {
                new ProcurementPlan
                {
                    ProjectId = project.Id,
                    SubComponentId = subComponents[0].Id,
                    ReferenceNo = "IDA-WSIP/NCB-W-001",
                    Description = "Строительство магистрального трубопровода Балхи-Дӯстӣ",
                    DescriptionTj = "Сохтмони қубури асосии Балхӣ-Дӯстӣ",
                    DescriptionEn = "Construction of Balkhi-Dusti main pipeline",
                    Method = ProcurementMethod.NCB,
                    Type = ProcurementType.Works,
                    EstimatedAmount = 2_500_000m,
                    Status = ProcurementStatus.Completed,
                    PlannedBidOpeningDate = DateTime.UtcNow.AddDays(-180),
                    ActualBidOpeningDate = DateTime.UtcNow.AddDays(-175)
                },
                new ProcurementPlan
                {
                    ProjectId = project.Id,
                    SubComponentId = subComponents[1].Id,
                    ReferenceNo = "IDA-WSIP/NCB-W-002",
                    Description = "Строительство распределительной сети в селе Намуна",
                    DescriptionTj = "Сохтмони шабакаи тақсимот дар деҳаи Намуна",
                    DescriptionEn = "Distribution network construction in Namuna village",
                    Method = ProcurementMethod.NCB,
                    Type = ProcurementType.Works,
                    EstimatedAmount = 1_800_000m,
                    Status = ProcurementStatus.Awarded,
                    PlannedBidOpeningDate = DateTime.UtcNow.AddDays(-120),
                    ActualBidOpeningDate = DateTime.UtcNow.AddDays(-115)
                },
                new ProcurementPlan
                {
                    ProjectId = project.Id,
                    SubComponentId = subComponents[2].Id,
                    ReferenceNo = "IDA-WSIP/ICB-G-003",
                    Description = "Поставка и установка насосного оборудования",
                    DescriptionTj = "Расонидан ва насб кардани таҷҳизоти насос",
                    DescriptionEn = "Supply and installation of pump equipment",
                    Method = ProcurementMethod.ICB,
                    Type = ProcurementType.Goods,
                    EstimatedAmount = 3_200_000m,
                    Status = ProcurementStatus.InProgress,
                    PlannedBidOpeningDate = DateTime.UtcNow.AddDays(-60)
                },
                new ProcurementPlan
                {
                    ProjectId = project.Id,
                    SubComponentId = subComponents[3].Id,
                    ReferenceNo = "IDA-WSIP/NCB-W-004",
                    Description = "Строительство очистных сооружений Вахш",
                    DescriptionTj = "Сохтмони иншооти тозакунии Вахш",
                    DescriptionEn = "Vakhsh water treatment plant construction",
                    Method = ProcurementMethod.NCB,
                    Type = ProcurementType.Works,
                    EstimatedAmount = 1_500_000m,
                    Status = ProcurementStatus.Planned,
                    PlannedBidOpeningDate = DateTime.UtcNow.AddDays(30)
                },
                new ProcurementPlan
                {
                    ProjectId = project.Id,
                    SubComponentId = subComponents[0].Id,
                    ReferenceNo = "IDA-WSIP/NCB-W-005",
                    Description = "Расширение системы водоснабжения Кушониён",
                    DescriptionTj = "Васеъшавии системаи обтаъминоти Қушониён",
                    DescriptionEn = "Kushoniyon water supply system expansion",
                    Method = ProcurementMethod.NCB,
                    Type = ProcurementType.Works,
                    EstimatedAmount = 2_000_000m,
                    Status = ProcurementStatus.Evaluation,
                    PlannedBidOpeningDate = DateTime.UtcNow.AddDays(-30)
                }
            };
            
            context.ProcurementPlans.AddRange(procurements);
            await context.SaveChangesAsync();
            
            // === DEMO DATA: Contractors ===
            var contractors = new[]
            {
                new Contractor
                {
                    Name = "LLC PMK-101",
                    ContactPerson = "Раҳмонов Ш.",
                    Phone = "+992 935 123456",
                    Email = "pmk101@mail.tj",
                    Country = "Таджикистан"
                },
                new Contractor
                {
                    Name = "LLC Tojik Energy",
                    ContactPerson = "Саидов А.",
                    Phone = "+992 937 234567",
                    Email = "tojik.energy@mail.tj",
                    Country = "Таджикистан"
                },
                new Contractor
                {
                    Name = "LLC Muhandis Binosoz",
                    ContactPerson = "Алиев М.",
                    Phone = "+992 918 345678",
                    Email = "muhandis@mail.tj",
                    Country = "Таджикистан"
                },
                new Contractor
                {
                    Name = "JV Balkh-2015 & Fayzi Javoni",
                    ContactPerson = "Холиқов Б.",
                    Phone = "+992 907 456789",
                    Email = "balkh2015@mail.tj",
                    Country = "Таджикистан"
                },
                new Contractor
                {
                    Name = "LLC Favvora",
                    ContactPerson = "Ғанизода Н.",
                    Phone = "+992 904 567890",
                    Email = "favvora@mail.tj",
                    Country = "Таджикистан"
                },
                new Contractor
                {
                    Name = "Shavadoon Construction Co., Ltd",
                    ContactPerson = "Kim Young",
                    Phone = "+82 10 1234 5678",
                    Email = "contact@shavadoon.com",
                    Country = "Южная Корея"
                }
            };
            
            context.Contractors.AddRange(contractors);
            await context.SaveChangesAsync();
            
            // === DEMO DATA: Contracts ===
            var now = DateTime.UtcNow;
            
            var contractsList = new List<Contract>
            {
                // Contract 1: COMPLETED (100% progress, past deadline)
                new Contract
                {
                    ContractNumber = "RWSSP-W/006-02 Lot2",
                    ScopeOfWork = "Строительство магистральных трубопроводов от водохранилища Дашти-Дили",
                    ProjectId = project.Id,
                    ContractorId = contractors[0].Id,
                    SigningDate = now.AddDays(-365),
                    ContractEndDate = now.AddDays(-121), // Completed 121 days ago
                    ContractAmount = 1364406.78m,
                    WorkCompletedPercent = 100
                },
                // Contract 2: IN PROGRESS but ON TRACK (67%, 155 days left)
                new Contract
                {
                    ContractNumber = "RWSSP-NCB-W/026-1",
                    ScopeOfWork = "Строительство распределительной сети в селе Намуна",
                    ProjectId = project.Id,
                    ContractorId = contractors[1].Id,
                    SigningDate = now.AddDays(-210),
                    ContractEndDate = now.AddDays(155),
                    ContractAmount = 238931.43m,
                    WorkCompletedPercent = 67
                },
                // Contract 3: ⚠️ AT RISK - Low progress (19%), only 25 days left - CRITICAL!
                new Contract
                {
                    ContractNumber = "RWWSSR-W/028-1",
                    ScopeOfWork = "Расширение водопроводной системы Кушониён - главный трубопровод",
                    ProjectId = project.Id,
                    ContractorId = contractors[5].Id, // Shavadoon - Korean company
                    SigningDate = now.AddDays(-365),
                    ContractEndDate = now.AddDays(10), // Only 10 days left!
                    ContractAmount = 8272049.46m,
                    WorkCompletedPercent = 19 // Only 19% done with 10 days left - CRITICAL!
                },
                // Contract 4: ACTIVE - good progress (70%)
                new Contract
                {
                    ContractNumber = "RWSSP-W/016 Lot1",
                    ScopeOfWork = "Строительство очистных сооружений Балхи",
                    ProjectId = project.Id,
                    ContractorId = contractors[3].Id,
                    SigningDate = now.AddDays(-300),
                    ContractEndDate = now.AddDays(258),
                    ContractAmount = 5179845.02m,
                    WorkCompletedPercent = 70
                },
                // Contract 5: ⚠️ Behind schedule (69%, only 20 days left)
                new Contract
                {
                    ContractNumber = "RWSSP-W/016 Lot2",
                    ScopeOfWork = "Строительство насосных станций Дӯстӣ",
                    ProjectId = project.Id,
                    ContractorId = contractors[4].Id,
                    SigningDate = now.AddDays(-288),
                    ContractEndDate = now.AddDays(20), // Only 20 days left with 69% progress
                    ContractAmount = 1393206.77m,
                    WorkCompletedPercent = 69
                }
            };
            
            context.Contracts.AddRange(contractsList);
            await context.SaveChangesAsync();
            
            // === DEMO DATA: Payments ===
            var payments = new[]
            {
                // Delayed Payment (will trigger PaymentDelay alert)
                new Payment
                {
                    ContractId = contractsList[3].Id,
                    Type = PaymentType.Interim,
                    Amount = 1500000m,
                    PaymentDate = now.AddDays(-20),
                    Status = PaymentStatus.Approved,
                    InvoiceNumber = "INV-2024-0087"
                },
                // Advance payment (paid)
                new Payment
                {
                    ContractId = contractsList[1].Id,
                    Type = PaymentType.Advance,
                    Amount = 47786.29m,
                    PaymentDate = now.AddDays(-60),
                    Status = PaymentStatus.Paid,
                    InvoiceNumber = "INV-2024-0045"
                },
                // Paid interim payment
                new Payment
                {
                    ContractId = contractsList[0].Id,
                    Type = PaymentType.Interim,
                    Amount = 272881.36m,
                    PaymentDate = now.AddDays(-90),
                    Status = PaymentStatus.Paid,
                    InvoiceNumber = "INV-2024-0023"
                },
                // Final payment (paid)
                new Payment
                {
                    ContractId = contractsList[0].Id,
                    Type = PaymentType.Final,
                    Amount = 136440.68m,
                    PaymentDate = now.AddDays(-45),
                    Status = PaymentStatus.Paid,
                    InvoiceNumber = "INV-2024-0056"
                },
                // Submitted for approval
                new Payment
                {
                    ContractId = contractsList[2].Id,
                    Type = PaymentType.Interim,
                    Amount = 827204.95m,
                    PaymentDate = now.AddDays(-5),
                    Status = PaymentStatus.Pending,
                    InvoiceNumber = "INV-2025-0002"
                },
                // Rejected payment
                new Payment
                {
                    ContractId = contractsList[4].Id,
                    Type = PaymentType.Interim,
                    Amount = 278641.35m,
                    PaymentDate = now.AddDays(-10),
                    Status = PaymentStatus.Rejected,
                    InvoiceNumber = "INV-2025-0001",
                    RejectionReason = "Несоответствие объёмов в АВР"
                }
            };
            context.Payments.AddRange(payments);
            await context.SaveChangesAsync();

            // === DEMO DATA: Tasks ===
            var admin = await userManager.FindByEmailAsync("admin@pmmis.tj");
            if (admin != null && !await context.ProjectTasks.AnyAsync())
            {
                var tasksList = new List<ProjectTask>
                {
                    // Overdue task - HIGH priority
                    new ProjectTask
                    {
                        Title = "Проверить АВР по контракту RWWSSR-W/028-1",
                        Description = "Необходимо проверить отчёт о прогрессе работ по расширению водопроводной системы Кушониён",
                        Status = ProjectTaskStatus.InProgress,
                        Priority = TaskPriority.High,
                        DueDate = now.AddDays(-12),
                        ContractId = contractsList[2].Id,
                        ProjectId = project.Id,
                        AssigneeId = admin.Id,
                        AssignedById = admin.Id
                    },
                    // Due soon task
                    new ProjectTask
                    {
                        Title = "Подготовить квартальный отчёт за Q4 2025",
                        Description = "Подготовить отчёт для Всемирного банка",
                        Status = ProjectTaskStatus.New,
                        Priority = TaskPriority.Normal,
                        DueDate = now.AddDays(5),
                        ProjectId = project.Id,
                        AssigneeId = admin.Id,
                        AssignedById = admin.Id
                    },
                    // Completed task
                    new ProjectTask
                    {
                        Title = "Провести приёмку работ по участку Балхи-Дӯстӣ",
                        Description = "Инспекция выполненных работ по магистральному трубопроводу",
                        Status = ProjectTaskStatus.Completed,
                        Priority = TaskPriority.High,
                        DueDate = now.AddDays(-30),
                        ContractId = contractsList[0].Id,
                        ProjectId = project.Id,
                        AssigneeId = admin.Id,
                        AssignedById = admin.Id
                    },
                    // Under review
                    new ProjectTask
                    {
                        Title = "Согласовать изменение в проекте насосной станции",
                        Description = "Подрядчик запросил изменение спецификации насосов",
                        Status = ProjectTaskStatus.UnderReview,
                        Priority = TaskPriority.Normal,
                        DueDate = now.AddDays(10),
                        ContractId = contractsList[3].Id,
                        ProjectId = project.Id,
                        AssigneeId = admin.Id,
                        AssignedById = admin.Id
                    },
                    // On hold
                    new ProjectTask
                    {
                        Title = "Закупка дополнительного оборудования",
                        Description = "Ожидание одобрения бюджета от Всемирного банка",
                        Status = ProjectTaskStatus.OnHold,
                        Priority = TaskPriority.Low,
                        DueDate = now.AddDays(45),
                        ProjectId = project.Id,
                        AssigneeId = admin.Id,
                        AssignedById = admin.Id
                    },
                    // Future task
                    new ProjectTask
                    {
                        Title = "Мониторинг качества воды в сети Намуна",
                        Description = "Провести тестирование качества воды после запуска распределительной сети",
                        Status = ProjectTaskStatus.New,
                        Priority = TaskPriority.Normal,
                        DueDate = now.AddDays(60),
                        ContractId = contractsList[1].Id,
                        ProjectId = project.Id,
                        AssigneeId = admin.Id,
                        AssignedById = admin.Id
                    },
                    // Urgent task
                    new ProjectTask
                    {
                        Title = "Решить проблему задержки платежа подрядчику",
                        Description = "Платёж по контракту RWSSP-W/016 Lot1 задержан более 15 дней",
                        Status = ProjectTaskStatus.InProgress,
                        Priority = TaskPriority.Critical,
                        DueDate = now.AddDays(2),
                        ContractId = contractsList[3].Id,
                        ProjectId = project.Id,
                        AssigneeId = admin.Id,
                        AssignedById = admin.Id
                    }
                };
                context.ProjectTasks.AddRange(tasksList);
                await context.SaveChangesAsync();
            }
        } // end of: if (!await context.Projects.AnyAsync())

        // === Indicator Categories (from Excel: Индикаторы WSIP-1.xlsx) ===
        if (!await context.Set<IndicatorCategory>().AnyAsync())
        {
                var categories = new[]
                {
                    new IndicatorCategory { Name = "Цели развития проекта (PDO)", SortOrder = 1 },
                    new IndicatorCategory { Name = "Промежуточные индикаторы результата", SortOrder = 2 }
                };
                context.Set<IndicatorCategory>().AddRange(categories);
                await context.SaveChangesAsync();
                
                // ============================
                // PDO Indicators (6 main + 2 sub)
                // ============================
                var pdoIndicators = new List<Indicator>
                {
                    // Row 8: Индикатор 1
                    new Indicator
                    {
                        Code = "PDO-1",
                        NameRu = "Люди, получившие доступ к безопасно управляемым услугам питьевого водоснабжения",
                        NameTj = "Одамоне, ки ба хизматрасонии бехатари обтаъминот дастрасӣ доранд",
                        NameEn = "People provided with access to safely managed drinking water services",
                        Unit = "количество (тыс.)",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 250000,
                        GeoDataSource = GeoDataSource.Population,
                        CategoryId = categories[0].Id,
                        SortOrder = 1
                    },
                    // Row 10: Индикатор 2
                    new Indicator
                    {
                        Code = "PDO-2",
                        NameRu = "Снижение времени, ежедневно затрачиваемого женщинами и девочками на доставку воды",
                        NameTj = "Кам шудани вақте, ки занон ва духтарон барои интиқоли об сарф мекунанд",
                        NameEn = "Reduction in time spent daily by women and girls on water collection",
                        Unit = "Процент",
                        MeasurementType = MeasurementType.Percentage,
                        TargetValue = 10,
                        CategoryId = categories[0].Id,
                        SortOrder = 2
                    },
                    // Row 11: Индикатор 3
                    new Indicator
                    {
                        Code = "PDO-3",
                        NameRu = "Доля контрольных проб воды, соответствующих национальным стандартам безопасности воды",
                        NameTj = "Ҳиссаи намунаҳои санҷишии об, ки ба стандартҳои миллии бехатарии об мувофиқат мекунанд",
                        NameEn = "Share of water quality control samples meeting national safety standards",
                        Unit = "Процент",
                        MeasurementType = MeasurementType.Percentage,
                        TargetValue = 95,
                        CategoryId = categories[0].Id,
                        SortOrder = 3
                    },
                    // Row 13: Индикатор 4
                    new Indicator
                    {
                        Code = "PDO-4",
                        NameRu = "При МЭВР создан и функционирует Отдел политики водоснабжения и водоотведения",
                        NameTj = "Дар назди ВЭОМ Шӯъбаи сиёсати обтаъминот ва обгузаронӣ таъсис ва фаъолият мекунад",
                        NameEn = "WSS Policy Department established and operational at MEWR",
                        Unit = "Да/Нет",
                        MeasurementType = MeasurementType.YesNo,
                        TargetValue = null,
                        CategoryId = categories[0].Id,
                        SortOrder = 4
                    },
                    // Row 15: Индикатор 5
                    new Indicator
                    {
                        Code = "PDO-5",
                        NameRu = "Целевые предприятия по водоснабжению в Хатлонской области приняли методы управления производственной и финансовой деятельностью, а также отчетности",
                        NameTj = "Корхонаҳои мақсадноки обтаъминот дар вилояти Хатлон усулҳои идоракунии фаъолияти истеҳсолӣ ва молиявӣ ва инчунин ҳисоботдиҳиро қабул карданд",
                        NameEn = "Target water utilities in Khatlon adopted operational, financial management and reporting methods",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 2,
                        CategoryId = categories[0].Id,
                        SortOrder = 5
                    },
                    // Row 16: Индикатор 6
                    new Indicator
                    {
                        Code = "PDO-6",
                        NameRu = "Дорожная карта по улучшению услуг водоснабжения и водоотведения одобрена для реализации",
                        NameTj = "Нақшаи роҳ оид ба беҳтар кардани хизматрасонии обтаъминот ва обгузаронӣ барои амалисозӣ тасдиқ шудааст",
                        NameEn = "Roadmap for improving WSS services approved for implementation",
                        Unit = "Да/Нет",
                        MeasurementType = MeasurementType.YesNo,
                        TargetValue = null,
                        CategoryId = categories[0].Id,
                        SortOrder = 6
                    }
                };
                
                context.Indicators.AddRange(pdoIndicators);
                await context.SaveChangesAsync();
                
                // PDO Sub-indicators
                var pdoSubIndicators = new[]
                {
                    // Row 9: Подиндикатор 1 к PDO-1
                    new Indicator
                    {
                        Code = "PDO-1.1",
                        NameRu = "Люди, получившие доступ к безопасно управляемым услугам питьевого водоснабжения - женщины",
                        NameTj = "Одамоне, ки ба хизматрасонии бехатари обтаъминот дастрасӣ доранд - занон",
                        NameEn = "People provided with access to safely managed drinking water services - women",
                        Unit = "количество (тыс.)",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 125000,
                        GeoDataSource = GeoDataSource.FemalePopulation,
                        ParentIndicatorId = pdoIndicators[0].Id,
                        CategoryId = categories[0].Id,
                        SortOrder = 1
                    },
                    // Row 14: Подиндикатор 1 к PDO-4
                    new Indicator
                    {
                        Code = "PDO-4.1",
                        NameRu = "Предприятия, предоставляющие информацию о КПЭ в ИСУ для сектора",
                        NameTj = "Корхонаҳое, ки маълумот дар бораи НАК ба ИСИ барои соҳа пешниҳод мекунанд",
                        NameEn = "Enterprises reporting KPIs to the sector MIS",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 4,
                        ParentIndicatorId = pdoIndicators[3].Id,
                        CategoryId = categories[0].Id,
                        SortOrder = 1
                    }
                };
                context.Indicators.AddRange(pdoSubIndicators);
                await context.SaveChangesAsync();
                
                // ============================
                // Intermediate Result Indicators (12 main + 7 sub)
                // ============================
                var irIndicators = new List<Indicator>
                {
                    // === Компонент 1: Укрепление потенциала учреждений ===
                    // Row 19: Индикатор 1
                    new Indicator
                    {
                        Code = "IR-1",
                        NameRu = "Общесекторальная информационная система мониторинга (ИСМ) функционирует и предоставляет отчеты по ключевым показателям эффективности целевых предприятий водоснабжения",
                        NameTj = "Системаи умумисоҳавии иттилоотии мониторинг (СИМ) фаъолият мекунад ва ҳисоботҳо оид ба нишондиҳандаҳои асосии самаранокии корхонаҳои мақсадноки обтаъминот пешниҳод мекунад",
                        NameEn = "Sector-wide MIS operational and reporting KPIs for target water utilities",
                        Unit = "Да/Нет",
                        MeasurementType = MeasurementType.YesNo,
                        TargetValue = null,
                        CategoryId = categories[1].Id,
                        SortOrder = 1
                    },
                    // Row 22: Индикатор 2
                    new Indicator
                    {
                        Code = "IR-2",
                        NameRu = "Лаборатории для проверки качества воды в целевых предприятиях по водоснабжению и в местных СГСЭН регулярно проводят проверки качества воды и информируют население о результатах таких проверок",
                        NameTj = "Лабораторияҳо барои санҷиши сифати об дар корхонаҳои мақсадноки обтаъминот ва дар ИХДСМ-и маҳаллӣ мунтазам санҷиши сифати обро мегузаронанд ва аҳолиро дар бораи натиҷаҳои ин санҷишҳо огоҳ мекунанд",
                        NameEn = "Water quality testing labs at target utilities and local SES regularly test water quality and inform population",
                        Unit = "Да/Нет",
                        MeasurementType = MeasurementType.YesNo,
                        TargetValue = null,
                        CategoryId = categories[1].Id,
                        SortOrder = 2
                    },
                    // Row 23: Индикатор 3
                    new Indicator
                    {
                        Code = "IR-3",
                        NameRu = "Модели тарифного регулирования для целевых предприятий по водоснабжению, разработаны и одобрены соответствующими органами",
                        NameTj = "Моделҳои танзими тарифӣ барои корхонаҳои мақсадноки обтаъминот, таҳия ва аз ҷониби мақомоти дахлдор тасдиқ шудаанд",
                        NameEn = "Tariff regulation models for target water utilities developed and approved by relevant authorities",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 4,
                        CategoryId = categories[1].Id,
                        SortOrder = 3
                    },
                    // Row 24: Индикатор 4
                    new Indicator
                    {
                        Code = "IR-4",
                        NameRu = "Для Хатлонской области разработан генеральный план водоснабжения и водоотведения, который отвечает потребностям адаптации к изменению климата",
                        NameTj = "Барои вилояти Хатлон нақшаи генералии обтаъминот ва обгузаронӣ таҳия шудааст, ки ба талаботи мутобиқшавӣ ба тағйирёбии иқлим ҷавобгӯ мебошад",
                        NameEn = "Master plan for WSS in Khatlon meets climate change adaptation needs",
                        Unit = "Да/Нет",
                        MeasurementType = MeasurementType.YesNo,
                        TargetValue = null,
                        CategoryId = categories[1].Id,
                        SortOrder = 4
                    },
                    // === Компонент 2: Инвестиции в водоснабжение и санитарию ===
                    // Row 26: Индикатор 5
                    new Indicator
                    {
                        Code = "IR-5",
                        NameRu = "Общая протяженность замененного трубопровода Вахшской межрайонной системы водоснабжения",
                        NameTj = "Дарозии умумии қубури иваз шудаи системаи байниноҳиягии обтаъминоти Вахш",
                        NameEn = "Total length of replaced pipeline of Vakhsh inter-district water supply system",
                        Unit = "Километры",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 50,
                        CategoryId = categories[1].Id,
                        SortOrder = 5
                    },
                    // Row 28: Индикатор 6
                    new Indicator
                    {
                        Code = "IR-6",
                        NameRu = "Новые подключения к центральным системам водоснабжения по итогам мероприятий в рамках Проекта",
                        NameTj = "Пайвастшавиҳои нав ба системаҳои марказии обтаъминот аз натиҷаҳои чорабиниҳо дар доираи Лоиҳа",
                        NameEn = "New connections to centralized water supply systems as a result of Project activities",
                        Unit = "Количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 30000,
                        GeoDataSource = GeoDataSource.Households,
                        CategoryId = categories[1].Id,
                        SortOrder = 6
                    },
                    // Row 29: Индикатор 7
                    new Indicator
                    {
                        Code = "IR-7",
                        NameRu = "Количество общественных учреждений с установленными базовыми санитарно-техническими сооружениями",
                        NameTj = "Миқдори муассисаҳои ҷамъиятӣ бо иншооти санитарию техникии асосии насбшуда",
                        NameEn = "Number of public institutions with basic sanitary facilities installed",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 50,
                        GeoDataSource = GeoDataSource.SchoolCount,
                        CategoryId = categories[1].Id,
                        SortOrder = 7
                    },
                    // Row 32: Индикатор 8
                    new Indicator
                    {
                        Code = "IR-8",
                        NameRu = "Количество людей, прошедших обучение по улучшению поведенческих практик и установок в отношении воды, санитарии и гигиены",
                        NameTj = "Миқдори одамоне, ки аз омӯзиш оид ба беҳтар кардани амалияҳо ва муносибатҳои рафторӣ нисбат ба об, санитария ва гигиена гузаштаанд",
                        NameEn = "Number of people trained on improving WASH behavioral practices and attitudes",
                        Unit = "Число (тыс.)",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 150,
                        CategoryId = categories[1].Id,
                        SortOrder = 8
                    },
                    // Row 34: Индикатор 9
                    new Indicator
                    {
                        Code = "IR-9",
                        NameRu = "Число людей, имеющих улучшенный доступ к основным услугам водоснабжения",
                        NameTj = "Шумораи одамоне, ки дастрасии беҳтар ба хизматрасонии асосии обтаъминот доранд",
                        NameEn = "Number of people with improved access to basic water supply services",
                        Unit = "Число (тыс.)",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 5,
                        GeoDataSource = GeoDataSource.Population,
                        CategoryId = categories[1].Id,
                        SortOrder = 9
                    },
                    // === Компонент 3: Управление проектом и мониторинг ===
                    // Row 36: Индикатор 10
                    new Indicator
                    {
                        Code = "IR-10",
                        NameRu = "Количество сотрудников ГУП \"ХМК\", МЭВР, ЦУП и предприятий по водоснабжению, прошедших обучение в рамках Проекта",
                        NameTj = "Миқдори кормандони КДД \"ХМК\", ВЭОМ, МИЛ ва корхонаҳои обтаъминот, ки дар доираи Лоиҳа омӯзиш гузаштаанд",
                        NameEn = "Number of SUE KMK, MEWR, PIU and water utility staff trained under the Project",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 300,
                        CategoryId = categories[1].Id,
                        SortOrder = 10
                    },
                    // Row 38: Индикатор 11
                    new Indicator
                    {
                        Code = "IR-11",
                        NameRu = "Доля бенефициаров Проекта, которые сообщают, что в рамках Проекта были развернуты эффективные процессы взаимодействия",
                        NameTj = "Ҳиссаи бенефициарҳои Лоиҳа, ки хабар медиҳанд, ки дар доираи Лоиҳа раванди самараноки ҳамкорӣ ҷорӣ карда шудааст",
                        NameEn = "Share of Project beneficiaries reporting effective engagement processes deployed",
                        Unit = "Процент",
                        MeasurementType = MeasurementType.Percentage,
                        TargetValue = 90,
                        CategoryId = categories[1].Id,
                        SortOrder = 11
                    },
                    // Row 39: Индикатор 12
                    new Indicator
                    {
                        Code = "IR-12",
                        NameRu = "Количество младших специалистов, нанятых в качестве стажеров целевыми предприятиями по водоснабжению",
                        NameTj = "Миқдори мутахассисони ҷавоне, ки ҳамчун коромӯз аз ҷониби корхонаҳои мақсадноки обтаъминот ба кор қабул шудаанд",
                        NameEn = "Number of junior specialists hired as interns by target water utilities",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 50,
                        CategoryId = categories[1].Id,
                        SortOrder = 12
                    }
                };
                
                context.Indicators.AddRange(irIndicators);
                await context.SaveChangesAsync();
                
                // Intermediate Result Sub-indicators
                var irSubIndicators = new[]
                {
                    // Row 20: Подиндикатор 1 к IR-1
                    new Indicator
                    {
                        Code = "IR-1.1",
                        NameRu = "Предприятия водоснабжения, предоставляющие информацию о КПЭ в отраслевую ИМС",
                        NameTj = "Корхонаҳои обтаъминот, ки маълумот оид ба НАК ба СИМ-и соҳавӣ пешниҳод мекунанд",
                        NameEn = "Water utilities reporting KPIs to the sector MIS",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 4,
                        ParentIndicatorId = irIndicators[0].Id,
                        CategoryId = categories[1].Id,
                        SortOrder = 1
                    },
                    // Row 21: Подиндикатор 2 к IR-1
                    new Indicator
                    {
                        Code = "IR-1.2",
                        NameRu = "Соответствующими органами разработаны и утверждены модели тарифного регулирования для целевых предприятий водоснабжения",
                        NameTj = "Аз ҷониби мақомоти дахлдор моделҳои танзими тарифӣ барои корхонаҳои мақсадноки обтаъминот таҳия ва тасдиқ шудаанд",
                        NameEn = "Tariff regulation models developed and approved by relevant authorities for target water utilities",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 4,
                        ParentIndicatorId = irIndicators[0].Id,
                        CategoryId = categories[1].Id,
                        SortOrder = 2
                    },
                    // Row 27: Подиндикатор 1 к IR-5
                    new Indicator
                    {
                        Code = "IR-5.1",
                        NameRu = "Протяженность замененного трубопровода для улучшения подачи воды потребителям и сокращения потерь воды в уязвимых к засухе районах",
                        NameTj = "Дарозии қубури иваз шуда барои беҳтар кардани расонидани об ба истеъмолгарон ва кам кардани талафоти об дар минтақаҳои осебпазир ба хушксолӣ",
                        NameEn = "Length of replaced pipeline for improved water delivery and reduced water losses in drought-vulnerable areas",
                        Unit = "Километры",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 16,
                        ParentIndicatorId = irIndicators[4].Id,
                        CategoryId = categories[1].Id,
                        SortOrder = 1
                    },
                    // Row 30: Подиндикатор 1 к IR-7
                    new Indicator
                    {
                        Code = "IR-7.1",
                        NameRu = "Количество школ с установленными базовыми санитарно-техническими сооружениями",
                        NameTj = "Миқдори мактабҳо бо иншооти санитарию техникии асосии насбшуда",
                        NameEn = "Number of schools with basic sanitary facilities installed",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 30,
                        ParentIndicatorId = irIndicators[6].Id,
                        CategoryId = categories[1].Id,
                        SortOrder = 1
                    },
                    // Row 31: Подиндикатор 2 к IR-7
                    new Indicator
                    {
                        Code = "IR-7.2",
                        NameRu = "Количество медицинских учреждений с установленными базовыми санитарно-техническими сооружениями",
                        NameTj = "Миқдори муассисаҳои тиббӣ бо иншооти санитарию техникии асосии насбшуда",
                        NameEn = "Number of medical facilities with basic sanitary facilities installed",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 20,
                        ParentIndicatorId = irIndicators[6].Id,
                        CategoryId = categories[1].Id,
                        SortOrder = 2
                    },
                    // Row 33: Подиндикатор 1 к IR-8
                    new Indicator
                    {
                        Code = "IR-8.1",
                        NameRu = "Количество женщин, прошедших обучение по улучшению поведенческих практик и установок в отношении воды, санитарии и гигиены",
                        NameTj = "Миқдори занон, ки аз омӯзиш оид ба беҳтар кардани амалияҳо ва муносибатҳои рафторӣ нисбат ба об, санитария ва гигиена гузаштаанд",
                        NameEn = "Number of women trained on improving WASH behavioral practices and attitudes",
                        Unit = "Число (тыс.)",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 75,
                        ParentIndicatorId = irIndicators[7].Id,
                        CategoryId = categories[1].Id,
                        SortOrder = 1
                    },
                    // Row 37: Подиндикатор 1 к IR-10
                    new Indicator
                    {
                        Code = "IR-10.1",
                        NameRu = "Количество женщин-сотрудников ГУП \"ХМК\", МЭВР, ЦУП и предприятий по водоснабжению, прошедших обучение в рамках Проекта",
                        NameTj = "Миқдори кормандони зан дар КДД \"ХМК\", ВЭОМ, МИЛ ва корхонаҳои обтаъминот, ки дар доираи Лоиҳа омӯзиш гузаштаанд",
                        NameEn = "Number of female staff of SUE KMK, MEWR, PIU and water utilities trained under the Project",
                        Unit = "количество",
                        MeasurementType = MeasurementType.Number,
                        TargetValue = 50,
                        ParentIndicatorId = irIndicators[9].Id,
                        CategoryId = categories[1].Id,
                        SortOrder = 1
                    }
                };
                context.Indicators.AddRange(irSubIndicators);
                await context.SaveChangesAsync();
                
                // === Sub-indicator for IR-12 ===
                var ir12Sub = new Indicator
                {
                    Code = "IR-12.1",
                    NameRu = "Количество женщин из категории младших специалистов, нанятых в качестве стажеров целевыми предприятиями по водоснабжению",
                    NameTj = "Миқдори занон аз категорияи мутахассисони ҷавон, ки ҳамчун коромӯз аз ҷониби корхонаҳои мақсадноки обтаъминот ба кор қабул шудаанд",
                    NameEn = "Number of female junior specialists hired as interns by target water utilities",
                    Unit = "количество",
                    MeasurementType = MeasurementType.Number,
                    TargetValue = 25,
                    ParentIndicatorId = irIndicators[11].Id,
                    CategoryId = categories[1].Id,
                    SortOrder = 1
                };
                context.Indicators.Add(ir12Sub);
                await context.SaveChangesAsync();
        }
    }
}

