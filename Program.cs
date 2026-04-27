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
    Console.WriteLine("❌ ERRO CRÍTICO: Connection string não encontrada!");
    Console.WriteLine("Configure a variável: ConnectionStrings__DefaultConnection");
    throw new Exception("Connection string não configurada");
}

// Log seguro (oculta a senha)
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
    Console.WriteLine($"✅ SSL Mode: {npgsqlConnString.SslMode}");
    Console.WriteLine($"✅ Trust Certificate: {npgsqlConnString.TrustServerCertificate}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro ao parsear string de conexão: {ex.Message}");
    throw;
}

// TESTE DE CONEXÃO DIRETA (antes do DbContext)
Console.WriteLine("\n🔍 TESTE DE CONEXÃO DIRETA (NpgsqlConnection)");
Console.WriteLine("----------------------------------------");

try
{
    using (var testConnection = new NpgsqlConnection(connectionString))
    {
        Console.WriteLine("🔄 Tentando abrir conexão direta...");
        await testConnection.OpenAsync();
        Console.WriteLine("✅✅✅ CONEXÃO DIRETA BEM-SUCEDIDA! ✅✅✅");
        
        // Testa uma query simples
        using (var cmd = new NpgsqlCommand("SELECT 1", testConnection))
        {
            var result = await cmd.ExecuteScalarAsync();
            Console.WriteLine($"✅ Query teste retornou: {result}");
        }
        
        await testConnection.CloseAsync();
    }
}
catch (PostgresException pgEx)
{
    Console.WriteLine($"❌ POSTGRES ERROR CÓDIGO: {pgEx.SqlState}");
    Console.WriteLine($"❌ Mensagem: {pgEx.MessageText}");
    Console.WriteLine($"❌ Detalhe: {pgEx.Detail}");
    Console.WriteLine($"❌ Dica: {pgEx.Hint}");
    Console.WriteLine($"❌ Posição: {pgEx.Position}");
}
catch (NpgsqlException npgEx)
{
    Console.WriteLine($"❌ NPGSLQ ERROR: {npgEx.Message}");
    Console.WriteLine($"❌ Código: {npgEx.Code}");
    Console.WriteLine($"❌ Inner: {npgEx.InnerException?.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERRO NA CONEXÃO DIRETA: {ex.GetType().Name}");
    Console.WriteLine($"❌ Mensagem: {ex.Message}");
    if (ex.InnerException != null)
    {
        Console.WriteLine($"❌ Inner Exception: {ex.InnerException.GetType().Name}");
        Console.WriteLine($"❌ Inner Message: {ex.InnerException.Message}");
    }
}

// Registra DbContext
Console.WriteLine("\n🔌 REGISTRANDO DbContext");
Console.WriteLine("----------------------------------------");

builder.Services.AddDbContext<EstudoDbContext>(options =>
{
    Console.WriteLine("⚙️ Configurando DbContext...");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(5);
        npgsqlOptions.CommandTimeout(30);
        Console.WriteLine("   - Retry on failure: 5");
        Console.WriteLine("   - Command timeout: 30s");
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

// ============================================================
// 🗄️ TESTE DO DbContext (com captura detalhada)
// ============================================================
Console.WriteLine("\n🔍 TESTANDO CONEXÃO DO DbContext");
Console.WriteLine("----------------------------------------");

try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EstudoDbContext>();
        Console.WriteLine("📡 DbContext obtido com sucesso");
        Console.WriteLine("🔄 Chamando CanConnectAsync()...");
        
        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync();
            
            if (canConnect)
            {
                Console.WriteLine("✅✅✅ CONEXÃO DO DbContext BEM-SUCEDIDA! ✅✅✅");
                
                // Tenta obter a versão
                try
                {
                    var version = await dbContext.Database.SqlQueryRaw<string>("SELECT version()").FirstOrDefaultAsync();
                    Console.WriteLine($"📊 PostgreSQL Version: {version}");
                }
                catch (Exception versionEx)
                {
                    Console.WriteLine($"⚠️ Erro ao obter versão: {versionEx.Message}");
                }
                
                // Aplica migrações
                Console.WriteLine("🔄 Verificando migrações...");
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                var pendingList = pendingMigrations.ToList();
                
                if (pendingList.Any())
                {
                    Console.WriteLine($"📋 Migrações pendentes: {pendingList.Count}");
                    foreach (var migration in pendingList)
                    {
                        Console.WriteLine($"   - {migration}");
                    }
                    
                    Console.WriteLine("🚀 Aplicando migrações...");
                    await dbContext.Database.MigrateAsync();
                    Console.WriteLine("✅ Migrações aplicadas!");
                }
                else
                {
                    Console.WriteLine("✅ Nenhuma migração pendente");
                }
            }
            else
            {
                Console.WriteLine("❌❌❌ FALHA NO CanConnectAsync() ❌❌❌");
                Console.WriteLine("O método retornou 'false' sem lançar exceção");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌❌❌ EXCEÇÃO NO CanConnectAsync() ❌❌❌");
            Console.WriteLine($"Tipo: {ex.GetType().Name}");
            Console.WriteLine($"Mensagem: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Tipo: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"Inner Mensagem: {ex.InnerException.Message}");
            }
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERRO NO ESCOPO: {ex.Message}");
}

Console.WriteLine("\n" + "=".PadRight(60, '='));
Console.WriteLine($"🚀 API RODANDO - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine($"🏥 Health check: http://localhost:8080/health");
Console.WriteLine("=".PadRight(60, '='));

await app.RunAsync();