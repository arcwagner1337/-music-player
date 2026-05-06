

using backendxd.Data;
using backendxd.Services;
using Microsoft.EntityFrameworkCore;
Environment.SetEnvironmentVariable("SLAVA_UKRAINI", "1");

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("NeonDB")));
builder.Services.AddScoped<GenerateJWT>();
builder.Services.AddScoped<mail>();
builder.Services.AddScoped<MusicService>();

// Add services to the container.

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

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
