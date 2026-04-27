using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Services;
using System.Text.Json.Serialization;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("=".PadRight(60, '='));
Console.WriteLine("🚀 INICIANDO REVISAFÁCIL API v1.3.2");
Console.WriteLine($"📅 Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine("=".PadRight(60, '='));

// Porta 8080 para o Render
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
    Console.WriteLine("✅ Kestrel configurado para porta 8080");
});

// Serviços básicos
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// 🔥 CONEXÃO COM POSTGRESQL
// ============================================================
Console.WriteLine("\n📡 CONFIGURANDO CONEXÃO COM BANCO DE DADOS");
Console.WriteLine("----------------------------------------");

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("❌ ERRO: Connection string não encontrada!");
    throw new Exception("Connection string não configurada");
}

// Log seguro
var safeString = System.Text.RegularExpressions.Regex.Replace(connectionString, "Password=.*?;", "Password=***;");
Console.WriteLine($"🔗 Connection String: {safeString}");

// Testa se a string é válida
try
{
    var npgsqlConnString = new NpgsqlConnectionStringBuilder(connectionString);
    Console.WriteLine($"✅ Host: {npgsqlConnString.Host}");
    Console.WriteLine($"✅ Porta: {npgsqlConnString.Port}");
    Console.WriteLine($"✅ Database: {npgsqlConnString.Database}");
    Console.WriteLine($"✅ Username: {npgsqlConnString.Username}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro na string: {ex.Message}");
    throw;
}

// TESTE DE CONEXÃO DIRETA
Console.WriteLine("\n🔍 TESTE DE CONEXÃO DIRETA");
Console.WriteLine("----------------------------------------");

try
{
    using (var testConnection = new NpgsqlConnection(connectionString))
    {
        Console.WriteLine("🔄 Tentando abrir conexão...");
        await testConnection.OpenAsync();
        Console.WriteLine("✅✅✅ CONEXÃO DIRETA BEM-SUCEDIDA! ✅✅✅");
        
        using (var cmd = new NpgsqlCommand("SELECT 1", testConnection))
        {
            var result = await cmd.ExecuteScalarAsync();
            Console.WriteLine($"✅ Query teste: {result}");
        }
        
        await testConnection.CloseAsync();
    }
}
catch (PostgresException pgEx)
{
    Console.WriteLine($"❌ POSTGRES ERROR: {pgEx.SqlState}");
    Console.WriteLine($"   Mensagem: {pgEx.MessageText}");
    Console.WriteLine($"   Detail: {pgEx.Detail}");
    Console.WriteLine($"   Hint: {pgEx.Hint}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERRO NA CONEXÃO DIRETA: {ex.GetType().Name}");
    Console.WriteLine($"   Mensagem: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
    }
}

// Registra DbContext
Console.WriteLine("\n🔌 REGISTRANDO DbContext");
builder.Services.AddDbContext<EstudoDbContext>(options =>
{
    Console.WriteLine("⚙️ Configurando DbContext...");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(5);
        npgsqlOptions.CommandTimeout(30);
    });
    options.UseLazyLoadingProxies();
    Console.WriteLine("✅ DbContext configurado");
});

builder.Services.AddScoped<IRevisaoService, RevisaoService>();
Console.WriteLine("✅ RevisaoService registrado");

// CORS
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});
Console.WriteLine("✅ CORS configurado");

// Build
Console.WriteLine("\n🏗️ Construindo aplicação...");
var app = builder.Build();
Console.WriteLine("✅ Aplicação construída");

// Pipeline
Console.WriteLine("\n🔧 Configurando pipeline...");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Endpoints
app.MapGet("/", () => "RevisaFácil API v1.3.2 Online");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Teste do DbContext
Console.WriteLine("\n🔍 TESTANDO DbContext");
Console.WriteLine("----------------------------------------");

try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EstudoDbContext>();
        Console.WriteLine("📡 Chamando CanConnectAsync...");
        
        var canConnect = await dbContext.Database.CanConnectAsync();
        
        if (canConnect)
        {
            Console.WriteLine("✅✅✅ CONEXÃO DO DbContext BEM-SUCEDIDA! ✅✅✅");
            
            // Aplica migrações
            Console.WriteLine("🔄 Aplicando migrações...");
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("✅ Migrações aplicadas!");
        }
        else
        {
            Console.WriteLine("❌❌❌ FALHA NO CanConnectAsync() ❌❌❌");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERRO NO DbContext: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"   Inner: {ex.InnerException.Message}");
}

Console.WriteLine("\n" + "=".PadRight(60, '='));
Console.WriteLine($"🚀 API RODANDO - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine("=".PadRight(60, '='));

await app.RunAsync();