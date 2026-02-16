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
builder.Services.AddScoped<PMMIS.Web.Services.IUserHierarchyService, PMMIS.Web.Services.UserHierarchyService>();
builder.Services.AddScoped<PMMIS.Web.Services.IDataAccessService, PMMIS.Web.Services.DataAccessService>();

// File Upload Service
builder.Services.AddScoped<PMMIS.Web.Services.IFileService, PMMIS.Web.Services.FileService>();

// Background Services
builder.Services.AddHostedService<PMMIS.Web.Services.DeadlineNotificationService>();
builder.Services.AddHostedService<PMMIS.Web.Services.NotificationQueueService>();

// Export Service
builder.Services.AddScoped<PMMIS.Web.Services.IExportService, PMMIS.Web.Services.ExportService>();

// Management Alerts (KPI) Service
builder.Services.AddScoped<PMMIS.Web.Services.IManagementAlertService, PMMIS.Web.Services.ManagementAlertService>();

// Currency Exchange Rate Service (NBT)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<PMMIS.Web.Services.INbtCurrencyService, PMMIS.Web.Services.NbtCurrencyService>();

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
    var supportedCultures = new[]
    {
        new CultureInfo("ru"),
        new CultureInfo("tg"), // Tajik
        new CultureInfo("en")
    };

    options.DefaultRequestCulture = new RequestCulture("ru");
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
