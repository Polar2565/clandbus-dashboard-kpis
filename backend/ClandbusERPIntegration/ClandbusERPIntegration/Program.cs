using ClandbusERPIntegration.Configurations;
using ClandbusERPIntegration.Interfaces;
using ClandbusERPIntegration.Services;
using ClandbusERPIntegration.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ======================================
// CONFIGURATION
// ======================================

builder.Services
    .AddOptions<AcumaticaSettings>()
    .Bind(builder.Configuration.GetSection("Acumatica"))
    .Validate(
        settings => Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _),
        "Acumatica:BaseUrl must be an absolute URL.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.Company),
        "Acumatica:Company is required.")
    .ValidateOnStart();

// ======================================
// ISOLATED ERP SESSIONS
// ======================================

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IAcumaticaSessionStore, AcumaticaSessionStore>();

builder.Services.AddPooledDbContextFactory<DashboardDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Dashboard")));

builder.Services.AddScoped<ISynchronizationService, SynchronizationService>();

// ======================================
// CONTROLLERS
// ======================================

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen();

// ======================================
// CORS
// ======================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AngularPolicy",
        policy =>
        {
            policy
                .WithOrigins(
                    "http://localhost:4200",
                    "http://127.0.0.1:4200"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
});

var app = builder.Build();

// ======================================
// PIPELINE
// ======================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AngularPolicy");

app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<DashboardDbContext>>();
    await using var db = await factory.CreateDbContextAsync();
    await db.Database.EnsureCreatedAsync();
}

app.Run();
