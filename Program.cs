using System.Globalization;
using System.Threading.RateLimiting;
using Fixora.Data;
using Fixora.Support;
using Fixora.Support.Authentication;
using Fixora.Support.Exceptions;
using Fixora.Support.Http;
using Fixora.Support.Middleware;
using Fixora.Support.Services;
using Fixora.Support.Services.Payments;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

FixoraConfig.Load(builder.Configuration.GetSection("Fixora"), builder.Environment.ContentRootPath);

var sessionMinutes = builder.Configuration.GetValue("Session:LifetimeMinutes", 120);

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseLazyLoadingProxies()
        .UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = builder.Configuration.GetValue("Session:CookieName", "fixora_session");
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromMinutes(sessionMinutes);
});

builder.Services.AddAntiforgery(options =>
{
    options.FormFieldName = "_token";
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "fixora_csrf";
});

var authentication = builder.Services.AddAuthentication();
foreach (var guard in Auth.Guards)
{
    authentication.AddCookie(guard, options =>
    {
        options.Cookie.Name = "fixora_" + guard;
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes);
        options.SlidingExpiration = true;
        options.LoginPath = guard == "admin" ? "/admin/login" : "/login";
        options.Events.OnRedirectToLogin = Authenticate.OnRedirectToLogin;
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            return Task.CompletedTask;
        };
    });
}

builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("verify.resend", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    options.AddPolicy("api", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddExceptionHandler<Handler>();
builder.Services.AddHostedService<Fixora.Support.Console.Kernel>();

builder.Services.AddScoped<PaymentGateway, FakeGateway>();
builder.Services.AddScoped<BookingDraft>();
builder.Services.AddScoped<AccountLookup>();
builder.Services.AddScoped<CodeService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<VerificationService>();
builder.Services.AddScoped<Fixora.Support.Services.BookingService>();
builder.Services.AddScoped<CashOtpService>();
builder.Services.AddScoped<DisputeService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<LoyaltyService>();
builder.Services.AddScoped<NotificationFeed>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ReviewService>();
builder.Services.AddScoped<SlotService>();
builder.Services.AddScoped<TechnicianSearch>();
builder.Services.AddScoped<DbSeeder>();

builder.Services
    .AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.ModelBindingMessageProvider.SetValueMustNotBeNullAccessor(_ => "This field is required.");
    })
    .AddRazorRuntimeCompilation();

var app = builder.Build();

AppPaths.WebRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
AppPaths.ContentRoot = app.Environment.ContentRootPath;
Current.Configure(app.Services.GetRequiredService<IHttpContextAccessor>());
CacheStore.Configure(app.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>());

var timezoneId = builder.Configuration.GetValue("App:Timezone", "Asia/Riyadh");
try
{
    Clock.Zone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
}
catch (TimeZoneNotFoundException)
{
    Clock.Zone = TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
}

if (!string.IsNullOrEmpty(FixoraConfig.Current.Now))
{
    Clock.SetTestNow(DateTime.Parse(FixoraConfig.Current.Now, CultureInfo.InvariantCulture));
}

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

if (args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<DbSeeder>().Run();
}

app.UseForwardedHeaders(TrustProxies.Options());

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/");
}

app.UseMiddleware<PreventRequestsDuringMaintenance>();
app.UseStaticFiles();
app.UseHttpMethodOverride(new HttpMethodOverrideOptions { FormFieldName = "_method" });
app.UseRouting();
app.UseMiddleware<KeepLoggedIn>();
app.UseSession();
app.UseMiddleware<TrimStrings>();
app.UseRateLimiter();
app.UseMiddleware<GuardAuthenticationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
