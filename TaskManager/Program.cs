using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TaskManager.Data;
using TaskManager.Data.Repositories;
using TaskManager.DTOs;
using TaskManager.Domain.Enums;
using TaskManager.Jobs;
using TaskManager.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// -------------------------------------------------------
// 1. BANCO DE DADOS — EF Core com PostgreSQL
// -------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString)
           .LogTo(Console.WriteLine, LogLevel.Information));

// -------------------------------------------------------
// 2. HANGFIRE
// -------------------------------------------------------
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// -------------------------------------------------------
// 3. INJEÇÃO DE DEPENDÊNCIA
// -------------------------------------------------------
// Scoped = uma instância por requisição HTTP
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ArchiveTasksJob>();

// -------------------------------------------------------
// 4. JWT AUTHENTICATION
// -------------------------------------------------------
var secretKey = builder.Configuration["Jwt:SecretKey"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// -------------------------------------------------------
// 5. SWAGGER COM SUPORTE A JWT
// -------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "TaskManager API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization. Digite: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer"),
            new List<string>()
        }
    });
});

// -------------------------------------------------------
var app = builder.Build();

// -------------------------------------------------------
// 6. APLICA MIGRATIONS AUTOMATICAMENTE AO SUBIR
// -------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// -------------------------------------------------------
// 7. MIDDLEWARES — A ORDEM IMPORTA
// -------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();

// Authentication ANTES de Authorization — sempre
app.UseAuthentication();
app.UseAuthorization();

// Dashboard do Hangfire — veja jobs rodando em /hangfire
app.UseHangfireDashboard("/hangfire");

// -------------------------------------------------------
// 8. HANGFIRE JOB RECORRENTE
// -------------------------------------------------------
// Cron.Daily() = todo dia à meia-noite
RecurringJob.AddOrUpdate<ArchiveTasksJob>(
    "archive-tasks",
    job => job.ExecuteAsync(),
    Cron.Daily());

// -------------------------------------------------------
// 9. ENDPOINTS
// -------------------------------------------------------

// --- AUTH (público — sem token) ---
var auth = app.MapGroup("/auth").WithTags("Auth");

auth.MapPost("/register", async (RegisterDto dto, IAuthService authService) =>
{
    var user = await authService.RegisterAsync(dto);
    return Results.Created($"/users/{user.Id}", user);
});

auth.MapPost("/login", async (LoginDto dto, IAuthService authService) =>
{
    var token = await authService.LoginAsync(dto);
    return Results.Ok(token);
});

// --- TASKS (protegido — exige token) ---
var tasks = app.MapGroup("/tasks")
    .WithTags("Tasks")
    .RequireAuthorization();

tasks.MapPost("/", async (TaskCreateDto dto, ITaskService taskService) =>
{
    var task = await taskService.CreateAsync(dto);
    return Results.Created($"/tasks/{task.Id}", task);
});

tasks.MapGet("/{id:int}", async (int id, ITaskService taskService) =>
{
    var task = await taskService.GetByIdAsync(id);
    return task is null ? Results.NotFound() : Results.Ok(task);
});

tasks.MapGet("/user/{userId:int}", async (int userId, ITaskService taskService) =>
{
    var list = await taskService.ListByUserAsync(userId);
    return Results.Ok(list);
});

tasks.MapPut("/{id:int}/status", async (
    int id,
    UpdateStatusDto dto,
    ITaskService taskService,
    ClaimsPrincipal user) =>
{
    // ClaimsPrincipal = dados do usuário logado extraídos do JWT
    var changedBy = user.Identity?.Name ?? "unknown";
    await taskService.UpdateStatusAsync(id, dto.NewStatus, changedBy);
    return Results.NoContent();
});

// Endpoint Dapper — relatório agregado
tasks.MapGet("/user/{userId:int}/summary", async (int userId, ITaskService taskService) =>
{
    var summary = await taskService.GetSummaryByUserAsync(userId);
    return Results.Ok(summary);
});

app.Run();