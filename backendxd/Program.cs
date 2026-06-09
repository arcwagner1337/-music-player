

using backendxd.Data;
using backendxd.Models;
using backendxd.Services;
using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;


string envPath = File.Exists(".env")
                 ? ".env"
                 : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\", ".env");

if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
}
else
{
    throw new Exception($"Файл .env не найден ни в папке сборки, ни в корне проекта по пути: {Path.GetFullPath(envPath)}");
}


var builder = WebApplication.CreateBuilder(args);



AppSettings.DbConString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING")
        ?? throw new Exception("Критическая ошибка: DB_CONNECTION_STRING не задан в .env!");

AppSettings.JwtKey = Environment.GetEnvironmentVariable("JWT_KEY")
        ?? throw new Exception("Критическая ошибка: JWT_KEY не задан в .env!");

AppSettings.MailSender = Environment.GetEnvironmentVariable("MAIL_SENDER")
        ?? throw new Exception("Критическая ошибка: MAIL_SENDER не задан в .env!");

AppSettings.MailAppKey = Environment.GetEnvironmentVariable("MAIL_APP_KEY")
        ?? throw new Exception("Критическая ошибка: MAIL_APP_KEY не задан в .env!");

AppSettings.LastFmApiKey = Environment.GetEnvironmentVariable("LASTFM_API_KEY")
        ?? throw new Exception("Критическая ошибка: LASTFM_API_KEY не задан в .env!");

AppSettings.WorkerUrl2 = Environment.GetEnvironmentVariable("WORKER_URL2")
        ?? throw new Exception("Критическая ошибка: WORKER_URL2 не задан в .env!");



Environment.SetEnvironmentVariable("SLAVA_UKRAINI", "1");


builder.Services.AddDbContext<AppDbContext>(options =>

    options.UseNpgsql(AppSettings.DbConString));

builder.Services.AddScoped<GenerateJWT>();
builder.Services.AddScoped<Mail>();

builder.Services.AddScoped<MusicService2>();
builder.Services.AddMemoryCache();

builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Введите: Bearer {ваш_токен}"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference {Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] {}
        }
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AppSettings.JwtKey))

        };


        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {

                Console.WriteLine($"[AUTH ERROR]: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnMessageReceived = context =>
            {

                var token = context.Request.Headers["Authorization"].ToString();
                Console.WriteLine($"[AUTH] Received header: {token}");
                return Task.CompletedTask;
            }
        };
    });




builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await InitDB.InitDatabase(app.Services);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.MapControllers();

app.Run();
