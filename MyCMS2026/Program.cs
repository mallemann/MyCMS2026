using MyCMS2026.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.PropertyNameCaseInsensitive = true);

// Cookie-Authentication
builder.Services.AddAuthentication("MyCMSCookies")
    .AddCookie("MyCMSCookies", options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
        options.Cookie.Name = ".MyCMS.Auth";
    });

builder.Services.AddAuthorization();

// Services
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<NavigationService>();
builder.Services.AddSingleton<SiteService>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<PendenzService>();
builder.Services.AddSingleton<RoleService>();
builder.Services.AddSingleton<TodoService>();
builder.Services.AddSingleton<MeetingService>();
builder.Services.AddSingleton<DownloadService>();
builder.Services.AddSingleton<OkrService>();
builder.Services.AddSingleton<KlassenService>();
builder.Services.AddSingleton<GruppenService>();
builder.Services.AddSingleton<VaultService>();
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<WeeklyMailService>();
builder.Services.AddSingleton<PccLinkService>();
builder.Services.AddHostedService<WeeklyMailBackgroundService>();

var app = builder.Build();

var pathBase = app.Configuration["PathBase"] ?? "";
if (!string.IsNullOrWhiteSpace(pathBase))
    app.UsePathBase(pathBase);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Setup-Middleware: leitet auf /Setup um solange App_Data/setup-complete fehlt
app.Use(async (ctx, next) =>
{
    var path      = ctx.Request.Path.Value ?? "";
    var setupFlag = Path.Combine(
        ctx.RequestServices.GetRequiredService<IWebHostEnvironment>().ContentRootPath,
        "App_Data", "setup-complete");

    if (!File.Exists(setupFlag) &&
        !path.StartsWith("/Setup", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/css",   StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/js",    StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/_",     StringComparison.OrdinalIgnoreCase))
    {
        ctx.Response.Redirect(ctx.Request.PathBase + "/Setup");
        return;
    }
    await next();
});

// Suchmaschinen-Indexierung verbieten
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();

// Offline-Sperre: Nicht-Administratoren werden blockiert wenn Status = "Offline"
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path.Value ?? "";

    // Account-Seiten und statische Dateien immer durchlassen
    if (!path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/css",     StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/js",      StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/img",     StringComparison.OrdinalIgnoreCase) &&
        !path.StartsWith("/_",       StringComparison.OrdinalIgnoreCase))
    {
        var siteSvc = ctx.RequestServices.GetRequiredService<SiteService>();
        var site    = await siteSvc.GetAsync();

        if (site.Status == "Offline")
        {
            var isAdmin = ctx.User.IsInRole("Administrator");
            if (!isAdmin)
            {
                ctx.Response.StatusCode = 503;
                ctx.Response.ContentType = "text/html; charset=utf-8";
                await ctx.Response.WriteAsync(@"<!DOCTYPE html>
<html lang='de'><head><meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1'/>
<title>Offline</title>
<link rel='stylesheet' href='/css/mycms.css'/>
</head><body class='login-body'>
<div class='login-wrapper'>
  <div class='login-brand-icon'><i class='bi bi-moon-stars-fill'></i></div>
  <div class='card login-card'>
    <div class='card-body p-4 text-center'>
      <h5 class='fw-bold mb-2'>Momentan offline</h5>
      <p class='text-muted'>Diese Anwendung ist vorübergehend nicht verfügbar.<br>Bitte versuchen Sie es später erneut.</p>
    </div>
  </div>
</div>
<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css'/>
</body></html>");
                return;
            }
        }
    }
    await next();
});

app.UseAuthorization();
app.MapRazorPages();

// Bilder aus App_Data/uploads/images/ ausliefern (persistent über Deploys)
app.MapGet("/img/{fileName}", (string fileName, IWebHostEnvironment env) =>
{
    // Sicherheit: nur erlaubte Zeichen im Dateinamen
    if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, @"^[\w\-\.]+$"))
        return Results.NotFound();

    var ext = Path.GetExtension(fileName).ToLowerInvariant();
    var contentType = ext switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png"            => "image/png",
        ".gif"            => "image/gif",
        ".webp"           => "image/webp",
        ".svg"            => "image/svg+xml",
        _                 => null
    };
    if (contentType is null) return Results.NotFound();

    var filePath = Path.Combine(env.ContentRootPath, "App_Data", "uploads", "images", fileName);
    return File.Exists(filePath)
        ? Results.File(filePath, contentType)
        : Results.NotFound();
}).RequireAuthorization();


app.Run();
