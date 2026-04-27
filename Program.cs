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

// Database configuration for PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 🔥 LOG DETALHADO (sem expor a senha completa)
Console.WriteLine("=== CONFIGURAÇÃO DO BANCO ===");
Console.WriteLine($"Connection String (com senha oculta): {connectionString?.Replace(connectionString?.Split(';').FirstOrDefault(p => p.Contains("Password")) ?? "", "Password=***")}");

builder.Services.AddDbContext<EstudoDbContext>((serviceProvider, options) =>
{
    try
    {
        Console.WriteLine("Configurando DbContext...");
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5);
            npgsqlOptions.CommandTimeout(30);
        });
        options.UseLazyLoadingProxies();
        Console.WriteLine("✅ DbContext configurado com sucesso");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERRO na configuração: {ex.Message}");
        throw;
    }
});

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

// 🔥 Tentativa de migração com diagnóstico completo
try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EstudoDbContext>();
        Console.WriteLine("Verificando banco de dados...");
        
        // Testa conexão com timeout
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationTokenSource.Token);
            if (canConnect)
            {
                Console.WriteLine("✅ Conexão com PostgreSQL OK!");
                
                // Tenta aplicar migrações
                try
                {
                    await dbContext.Database.MigrateAsync();
                    Console.WriteLine("✅ Migrações aplicadas com sucesso!");
                }
                catch (Exception migrationEx)
                {
                    Console.WriteLine($"⚠️ Erro nas migrações: {migrationEx.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌ Falha na conexão com PostgreSQL");
                Console.WriteLine("Verifique: 1) Host correto 2) Porta 5432 aberta 3) Senha correta 4) SSL Mode");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("❌ Timeout ao conectar (10 segundos) - Supabase pode estar bloqueando");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro de conexão detalhado: {ex.GetType().Name}: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"   Inner: {ex.InnerException.Message}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ ERRO no escopo do banco: {ex.Message}");
    Console.WriteLine("A API continuará rodando, mas funcionalidades do banco falharão");
}

Console.WriteLine($"🚀 API rodando em: http://localhost:8080");
await app.RunAsync();