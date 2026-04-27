using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Services;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// 🔥 CRÍTICO: Força a porta 8080 para o Render
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// 🔥 REMOVEMOS AddOpenApi() - não existe no .NET 8
// Mantemos apenas Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database configuration for PostgreSQL (com retry e logging)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 🔥 Log da string de conexão (sem a senha completa)
Console.WriteLine($"Tentando conectar ao PostgreSQL... Host: {connectionString?.Split(';').FirstOrDefault(s => s.Contains("Host"))}");

builder.Services.AddDbContext<EstudoDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
        npgsqlOptions.EnableRetryOnFailure(5))  // Tenta 5 vezes antes de falhar
        .UseLazyLoadingProxies());

// Register services
builder.Services.AddScoped<IRevisaoService, RevisaoService>();

// 🔥 CORS mais permissivo para testes
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 🔥 Log de inicialização
Console.WriteLine("=== Iniciando RevisaFácil API ===");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection(); // 🔥 COMENTE ou remova - o Render já lida com SSL
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoints
app.MapGet("/", () => "RevisaFácil API v1.3.2 Online");
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName 
}));

// 🔥 Tentativa de migração com try-catch para não quebrar o app
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EstudoDbContext>();
        Console.WriteLine("Verificando banco de dados...");
        var canConnect = await dbContext.Database.CanConnectAsync();
        if (canConnect)
        {
            Console.WriteLine("✅ Conexão com PostgreSQL OK!");
            // Aplica migrações se existirem
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("✅ Migrações aplicadas!");
        }
        else
        {
            Console.WriteLine("⚠️ Não foi possível conectar ao PostgreSQL");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ ERRO no banco de dados: {ex.Message}");
    Console.WriteLine("A API continuará rodando, mas algumas funcionalidades podem falhar");
}

Console.WriteLine($"🚀 API rodando em: http://localhost:8080");
await app.RunAsync();