using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Context;
using oop_s2_2_mvc_83303.Data;

var builder = WebApplication.CreateBuilder(args);

// --- Task 1: Enhanced Serilog Configuration ---
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    // Dynamic enrichment with environment name
    .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
    .Enrich.WithProperty("Application", "FoodSafetyTracker")
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Database & Identity Setup
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=food_safety.db";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDefaultIdentity<IdentityUser>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequiredLength = 4;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Seed the database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DbInitializer.Seed(services);
}

// Global Error Handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Middleware to enrich Serilog with UserName from HttpContext
app.Use(async (context, next) =>
{
    var userName = context.User.Identity?.IsAuthenticated == true 
        ? context.User.Identity.Name 
        : "Anonymous";
    using (LogContext.PushProperty("UserName", userName))
    {
        await next.Invoke();
    }
});

app.UseAuthentication();
app.UseAuthorization();

// Task 3: Log Warning if a user attempts to access an unauthorized page (interceptor for 403)
app.Use(async (context, next) =>
{
    await next();
    if (context.Response.StatusCode == 403)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        var userName = context.User.Identity?.IsAuthenticated == true ? context.User.Identity.Name : "Anonymous";
        logger.LogWarning("Access Denied (403): User {User} attempted to access {Path}", userName, context.Request.Path);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

try
{
    Log.Information("Food Safety Tracker starting up in {Environment} mode", builder.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
