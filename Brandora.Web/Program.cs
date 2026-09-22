using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Brandora.Web.Services.Email;
using Brandora.Web.Services.Payments;

DotNetEnv.Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // RequireConfirmedAccount stays false — Identity's built-in email-confirmation link flow
        // isn't used. Brandora uses its own 6-digit code + admin-approval gate instead, enforced
        // manually in AccountController's Login action (EmailConfirmed + VerificationStatus checks).
        options.SignIn.RequireConfirmedAccount = false;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    // Logout / password change (which bumps the SecurityStamp) takes effect on other
    // devices within this interval, not just on next full cookie expiry.
    options.ValidationInterval = TimeSpan.FromMinutes(1);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";

    // Sliding session: renews on activity, only expires after this long with NO activity —
    // matches how mainstream sites behave, not a fixed calendar cutoff (2026-09-17 decision).
    options.ExpireTimeSpan = TimeSpan.FromDays(60);
    options.SlidingExpiration = true;

    options.Cookie.Name = "Brandora.Auth";
    options.Cookie.HttpOnly = true;
    // SameAsRequest (not Always) — the app has a plain-HTTP local dev profile; in production
    // (Render, HTTPS-only) requests always arrive as HTTPS so the cookie is still Secure there.
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<MediaUploadService>();
builder.Services.AddScoped<AgreementService>();
builder.Services.AddScoped<AdminAuthService>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();

// Real checkout providers the Brand can choose between: bKash Tokenized Checkout and
// Nagad Online Payment. Each is a typed HttpClient (base address/handler lifetime managed
// by the factory), exposed as an IPaymentGateway so PaymentSettlementService receives
// all of them and routes each payment to the one the Brand picked.
builder.Services.AddHttpClient<BkashGateway>();
builder.Services.AddHttpClient<NagadGateway>();
builder.Services.AddTransient<IPaymentGateway>(sp => sp.GetRequiredService<BkashGateway>());
builder.Services.AddTransient<IPaymentGateway>(sp => sp.GetRequiredService<NagadGateway>());
builder.Services.AddScoped<WalletService>();
builder.Services.AddScoped<PaymentSettlementService>();

builder.Services.AddAuthentication()
    .AddCookie("AdminScheme", options =>
    {
        options.LoginPath = "/Admin/AdminAccount/Login";
        options.AccessDeniedPath = "/Admin/AdminAccount/Login";
        options.Cookie.Name = "Brandora.Admin";
    });

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// The default UseHttpsRedirection() can't pick an https port on its own when the app is
// bound to more than one (e.g. running the Brand/Influencer ports 5130/7150 and the Admin
// ports 5140/7160 together, via `dotnet run --urls "...;...;...;..."` for local testing) —
// it throws InvalidOperationException. Redirect explicitly instead, keeping each http port
// paired with its own https port so Admin and the public site both still redirect correctly.
var httpToHttpsPort = new Dictionary<int, int> { [5130] = 7150, [5140] = 7160 };
app.Use(async (context, next) =>
{
    if (!context.Request.IsHttps && httpToHttpsPort.TryGetValue(context.Connection.LocalPort, out var httpsPort))
    {
        var redirectUrl = $"https://{context.Request.Host.Host}:{httpsPort}{context.Request.Path}{context.Request.QueryString}";
        context.Response.Redirect(redirectUrl, permanent: false);
        return;
    }
    await next();
});
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

var adminPorts = app.Configuration.GetSection("AdminPorts").Get<int[]>() ?? [];
if (adminPorts.Length > 0)
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/" && adminPorts.Contains(context.Connection.LocalPort))
        {
            context.Response.Redirect("/Admin/AdminAccount/Login");
            return;
        }

        await next();
    });
}

app.MapStaticAssets();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=AdminAccount}/{action=Login}/{id?}")
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
