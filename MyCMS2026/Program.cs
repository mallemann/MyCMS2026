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

// Suchmaschinen-Indexierung verbieten
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
