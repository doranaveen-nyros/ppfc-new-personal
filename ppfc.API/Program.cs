using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ppfc.API.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Load JWT settings
var configuration = builder.Configuration;

// Configure Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,                     // Check token issuer
            ValidateAudience = true,                   // Check token audience
            ValidateLifetime = true,                   // Check token expiration
            ValidateIssuerSigningKey = true,           // Validate secret key
            ValidIssuer = configuration["Jwt:Issuer"],
            ValidAudience = configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization(); // Role/claim-based authorization

builder.Services.AddMemoryCache();                     // for IMemoryCache
builder.Services.AddHttpClient<SmsService>();          // injects HttpClient
builder.Services.AddScoped<SmsService>();             // injects IConfiguration automatically
builder.Services.AddScoped<ClosingBalanceService>();

// Enable console logging explicitly
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication(); // Must come BEFORE UseAuthorization

app.UseAuthorization();

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
