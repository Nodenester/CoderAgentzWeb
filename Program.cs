using CoderAgentzWeb.Components;
using CoderAgentzWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables
DotNetEnv.Env.Load();

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<TemplateService>();
builder.Services.AddScoped<AgentTaskService>();
builder.Services.AddScoped<GitHubService>();

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "GitHub";
})
.AddCookie(options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
})
.AddGitHub(options =>
{
    options.ClientId = Environment.GetEnvironmentVariable("GITHUB_CLIENT_ID") ?? "";
    options.ClientSecret = Environment.GetEnvironmentVariable("GITHUB_CLIENT_SECRET") ?? "";
    options.Scope.Add("read:user");
    options.Scope.Add("repo");
    options.SaveTokens = true;
    options.Events.OnCreatingTicket = context =>
    {
        if (context.AccessToken != null)
        {
            context.Identity?.AddClaim(new Claim("access_token", context.AccessToken));
        }
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Auth endpoints
app.MapGet("/login", async context =>
{
    await context.ChallengeAsync("GitHub", new AuthenticationProperties
    {
        RedirectUri = "/"
    });
});

app.MapGet("/logout", async context =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    context.Response.Redirect("/");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
