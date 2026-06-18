using DotNetEnv;
using System.Text;
using Microsoft.OpenApi.Models;
using AuthService.AuthDbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using AuthService.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURATION (ENV)
var envPath = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env")
);
Env.Load(envPath);

Console.WriteLine($"Loading .env from: {envPath}");
Console.WriteLine($"DB_HOST: {Environment.GetEnvironmentVariable("DB_HOST")}");
Console.WriteLine($"DB_PORT: {Environment.GetEnvironmentVariable("DB_PORT")}");
Console.WriteLine($"DB_NAME: {Environment.GetEnvironmentVariable("DB_NAME")}");
Console.WriteLine($"DB_USER: {Environment.GetEnvironmentVariable("DB_USER")}");
Console.WriteLine($"DB_USER: {Environment.GetEnvironmentVariable("ISSUER")}");
Console.WriteLine($"DB_USER: {Environment.GetEnvironmentVariable("AUDIENCE")}");

// INSTANCE OF JWTSETTINGS
var jwtSettings = new JwtSettings
{
    Issuer = Environment.GetEnvironmentVariable("ISSUER")!,
    Audience = Environment.GetEnvironmentVariable("AUDIENCE")!,
    Key = Environment.GetEnvironmentVariable("KEY")!,
    Expiration = int.Parse(Environment.GetEnvironmentVariable("EXPIRATIONMINUTES")!)
};

// JWT AUTHENTICATION
builder.Services.AddAuthentication(
    JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters =
        new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer =
               jwtSettings.Issuer,

            ValidAudience =
                jwtSettings.Audience,

            IssuerSigningKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(
                        jwtSettings.Key)),

        };
});

// DATABASE 
var connectionString =     
    $"Host={Environment.GetEnvironmentVariable("DB_HOST")};" +
    $"Port={Environment.GetEnvironmentVariable("DB_PORT")};" +
    $"Database={Environment.GetEnvironmentVariable("DB_NAME")};" +
    $"Username={Environment.GetEnvironmentVariable("DB_USER")};" +
    $"Password={Environment.GetEnvironmentVariable("DB_PASSWORD")};";
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(connectionString)
);

// SERVICES - CONTROLLERS
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// SWAGGER
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Ingrese: Bearer {token}"
        });

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
});

// CUSTOM SERVICES (DI)
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddSingleton(jwtSettings);


// PIPELINE MIDDLEWARE
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();