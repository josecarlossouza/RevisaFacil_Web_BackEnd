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
// 🔥 CONEXÃO COM POSTGRESQL - String direta do Supabase
// ============================================================
Console.WriteLine("\n📡 CONFIGURANDO CONEXÃO COM BANCO DE DADOS");
Console.WriteLine("----------------------------------------");

// PEGA A STRING COMPLETA das variáveis de ambiente
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("❌ ERRO: Connection string não encontrada!");
    Console.WriteLine("Configure a variável: ConnectionStrings__DefaultConnection");
}
else
{
    // Log seguro (oculta a senha)
    var safeString = System.Text.RegularExpressions.Regex.Replace(connectionString, "Password=.*?;", "Password=***;");
    Console.WriteLine($"🔗 Connection String: {safeString}");
    
    // Testa se é válida
    try
    {
        var npgsqlConnString = new NpgsqlConnectionStringBuilder(connectionString);
        Console.WriteLine($"✅ Host: {npgsqlConnString.Host}, Porta: {npgsqlConnString.Port}, SSL: {npgsqlConnString.SslMode}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Erro na string: {ex.Message}");
    }
}

// Registra DbContext
builder.Services.AddDbContext<EstudoDbContext>(options =>
{
    Console.WriteLine("\n⚙️ Configurando DbContext...");
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

// Build da aplicação
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

// Teste de conexão com o banco
Console.WriteLine("\n🔍 TESTANDO CONEXÃO COM POSTGRESQL");
Console.WriteLine("----------------------------------------");

try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EstudoDbContext>();
        Console.WriteLine("📡 Verificando conexão...");
        
        var canConnect = await dbContext.Database.CanConnectAsync();
        
        if (canConnect)
        {
            Console.WriteLine("✅✅✅ CONEXÃO COM POSTGRESQL BEM-SUCEDIDA! ✅✅✅");
            
            // Aplica migrações
            await dbContext.Database.MigrateAsync();
            Console.WriteLine("✅ Migrações aplicadas com sucesso!");
        }
        else
        {
            Console.WriteLine("❌❌❌ FALHA NA CONEXÃO COM POSTGRESQL ❌❌❌");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERRO DE CONEXÃO: {ex.Message}");
    if (ex.InnerException != null)
        Console.WriteLine($"   Detalhe: {ex.InnerException.Message}");
}

Console.WriteLine("\n" + "=".PadRight(60, '='));
Console.WriteLine($"🚀 API RODANDO - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine("=".PadRight(60, '='));

await app.RunAsync();