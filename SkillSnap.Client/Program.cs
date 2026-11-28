using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SkillSnap.Client;
using SkillSnap.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddressSetting = builder.Configuration["ApiBaseAddress"];
if (!Uri.TryCreate(apiBaseAddressSetting, UriKind.Absolute, out var apiBaseAddress))
{
    throw new InvalidOperationException("The ApiBaseAddress configuration must be an absolute URI.");
}

builder.Services.AddScoped<UserSessionService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddSingleton<AccessTokenStore>();
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient("AuthApi", client => client.BaseAddress = apiBaseAddress);
builder.Services.AddHttpClient("Api", client => client.BaseAddress = apiBaseAddress)
    .AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<SkillService>();

await builder.Build().RunAsync();
