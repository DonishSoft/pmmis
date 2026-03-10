using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using PMMIS.Domain.Entities;
using PMMIS.Infrastructure.Data;
using Serilog;
using Syncfusion.Licensing;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Register Syncfusion license from configuration
var syncfusionLicense = builder.Configuration["Syncfusion:LicenseKey"];
if (!string.IsNullOrEmpty(syncfusionLicense))
{
    SyncfusionLicenseProvider.RegisterLicense(syncfusionLicense);
}

// Add services to the container.
builder.Services.AddControllersWithViews();

// Allow large file uploads (for Excel import)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100_000_000; // 100MB
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(10);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(5);
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100_000_000;
});

// Syncfusion license is registered above, EJ2 components are loaded via CDN in views

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity
builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Menu Permission Service
builder.Services.AddScoped<PMMIS.Web.Services.IMenuPermissionService, PMMIS.Web.Services.MenuPermissionService>();

// Authorization Handler for granular permissions
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PMMIS.Web.Authorization.MenuPermissionHandler>();

// HttpClient for external APIs (Telegram, etc.)
builder.Services.AddHttpClient();

// Notification Services
builder.Services.AddScoped<PMMIS.Web.Services.INotificationService, PMMIS.Web.Services.NotificationService>();
builder.Services.AddScoped<PMMIS.Web.Services.IEmailSender, PMMIS.Web.Services.EmailSender>();
builder.Services.AddScoped<PMMIS.Web.Services.ITelegramSender, PMMIS.Web.Services.TelegramSender>();

// Task Management Service
builder.Services.AddScoped<PMMIS.Web.Services.ITaskService, PMMIS.Web.Services.TaskService>();
builder.Services.AddScoped<PMMIS.Web.Services.IAuditService, PMMIS.Web.Services.AuditService>();
builder.Services.AddScoped<PMMIS.Web.Services.IExcelImportService, PMMIS.Web.Services.ExcelImportService>();
builder.Services.AddScoped<PMMIS.Web.Services.IAiImportService, PMMIS.Web.Services.AiImportService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<PMMIS.Web.Services.IPermissionService, PMMIS.Web.Services.PermissionService>();
builder.Services.AddScoped<PMMIS.Web.Services.IUserHierarchyService, PMMIS.Web.Services.UserHierarchyService>();
builder.Services.AddScoped<PMMIS.Web.Services.IDataAccessService, PMMIS.Web.Services.DataAccessService>();

// Workflow Routing Service (approval chain)
builder.Services.AddScoped<PMMIS.Web.Services.IWorkflowRoutingService, PMMIS.Web.Services.WorkflowRoutingService>();

// File Upload Service
builder.Services.AddScoped<PMMIS.Web.Services.IFileService, PMMIS.Web.Services.FileService>();

// Background Services
builder.Services.AddHostedService<PMMIS.Web.Services.DeadlineNotificationService>();
builder.Services.AddHostedService<PMMIS.Web.Services.NotificationQueueService>();
builder.Services.AddHostedService<PMMIS.Web.Services.ProcurementDeadlineService>();

// Export Service
builder.Services.AddScoped<PMMIS.Web.Services.IExportService, PMMIS.Web.Services.ExportService>();

// Management Alerts (KPI) Service
builder.Services.AddScoped<PMMIS.Web.Services.IManagementAlertService, PMMIS.Web.Services.ManagementAlertService>();

// Currency Exchange Rate Service (NBT)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<PMMIS.Web.Services.INbtCurrencyService, PMMIS.Web.Services.NbtCurrencyService>();
builder.Services.AddHostedService<PMMIS.Web.Services.CurrencyRateFetchJob>();
builder.Services.AddHostedService<PMMIS.Web.Services.BackfillCurrencyRatesJob>();

// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// Localization
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    // Override decimal separator to "." for HTML <input type="number"> compatibility
    // Russian and Tajik cultures use "," by default which breaks browser number inputs
    var ruCulture = new CultureInfo("ru");
    ruCulture.NumberFormat.NumberDecimalSeparator = ".";
    ruCulture.NumberFormat.CurrencyDecimalSeparator = ".";

    var tgCulture = new CultureInfo("tg");
    tgCulture.NumberFormat.NumberDecimalSeparator = ".";
    tgCulture.NumberFormat.CurrencyDecimalSeparator = ".";

    var supportedCultures = new[] { ruCulture, tgCulture, new CultureInfo("en") };

    options.DefaultRequestCulture = new RequestCulture(ruCulture);
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Localization middleware
app.UseRequestLocalization();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

// Seed data on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        
        await context.Database.MigrateAsync();
        await SeedData.InitializeAsync(context, userManager, roleManager);
        await SeedDataGeo.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();
