using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.DataProtection;
using ppfc.web.Data;
using ppfc.web.Helpers;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

var apiGatewayBaseUrl = builder.Configuration["ApiGateway:BaseUrl"] // Getting the API Gateway Base URL
                        ?? "https://localhost:5114";

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

builder.Services.AddHttpClient();
builder.Services.AddRadzenComponents();

// Configure HttpClient for API calls with a base address
builder.Services.AddHttpClient("FinanceAPI", client =>
{
    client.BaseAddress = new Uri(apiGatewayBaseUrl);
});

// Register HttpClient for dependency injection
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("FinanceAPI"); // This ensures [Inject] HttpClient has BaseAddress
});

// Add secure session storage
builder.Services.AddScoped<ProtectedSessionStorage>();

builder.Services.AddScoped<ProtectedLocalStorage>();


// Register the Authentication Provider
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddAuthorizationCore();

// Persist DataProtection keys to a folder so session survives restarts
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys")))
    .SetApplicationName("ppfc.web");

//Register AppNotifier for notifications
builder.Services.AddScoped<AppNotifier>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();

app.MapFallbackToPage("/_Host");

app.Run();
