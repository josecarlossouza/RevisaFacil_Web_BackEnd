using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Services;
using System.Text.Json.Serialization;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

Console.WriteLine("=" .PadRight(60, '='));
Console.WriteLine("🚀 INICIANDO REVISAFÁCIL API v1.3.2");
Console.WriteLine($"📅 Timestamp: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine("=" .PadRight(60, '='));

// ============================================================
// 🔥 PORTA - Forçar 8080 para o Render
// ============================================================
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
    Console.WriteLine("✅ Kestrel configurado para escutar na porta 8080");
});

// ============================================================
// 📦 SERVIÇOS BÁSICOS
// ============================================================
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ============================================================
// 🗄️ CONEXÃO COM POSTGRESQL - Usando variáveis de ambiente
// ============================================================
Console.WriteLine("\n📡 CONFIGURANDO CONEXÃO COM BANCO DE DADOS");
Console.WriteLine("----------------------------------------");

// Lê variáveis de ambiente
var dbHost = Environment.GetEnvironmentVariable("DB_HOST");
var dbPort = Environment.GetEnvironmentVariable("DB_PORT");
var dbName = Environment.GetEnvironmentVariable("DB_NAME");
var dbUser = Environment.GetEnvironmentVariable("DB_USER");
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

// Log das variáveis (sem expor senha)
Console.WriteLine($"DB_HOST: {(string.IsNullOrEmpty(dbHost) ? "❌ NÃO DEFINIDO" : dbHost)}");
Console.WriteLine($"DB_PORT: {(string.IsNullOrEmpty(dbPort) ? "❌ NÃO DEFINIDO" : dbPort)}");
Console.WriteLine($"DB_NAME: {(string.IsNullOrEmpty(dbName) ? "❌ NÃO DEFINIDO" : dbName)}");
Console.WriteLine($"DB_USER: {(string.IsNullOrEmpty(dbUser) ? "❌ NÃO DEFINIDO" : dbUser)}");
Console.WriteLine($"DB_PASSWORD: {(string.IsNullOrEmpty(dbPassword) ? "❌ NÃO DEFINIDO" : "✅ DEFINIDO (oculto)")}");

// Teste de DNS
try
{
    Console.WriteLine($"\n🔍 Testando resolução DNS para {dbHost}...");
    var addresses = System.Net.Dns.GetHostAddresses(dbHost);
    Console.WriteLine($"✅ DNS resolvido: {string.Join(", ", addresses.Select(a => a.ToString()))}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Falha na resolução DNS: {ex.Message}");
}

// Validação
var missingVars = new List<string>();
if (string.IsNullOrEmpty(dbHost)) missingVars.Add("DB_HOST");
if (string.IsNullOrEmpty(dbPort)) missingVars.Add("DB_PORT");
if (string.IsNullOrEmpty(dbName)) missingVars.Add("DB_NAME");
if (string.IsNullOrEmpty(dbUser)) missingVars.Add("DB_USER");
if (string.IsNullOrEmpty(dbPassword)) missingVars.Add("DB_PASSWORD");

if (missingVars.Any())
{
    Console.WriteLine($"\n❌ ERRO CRÍTICO: Variáveis de ambiente não definidas: {string.Join(", ", missingVars)}");
    Console.WriteLine("⚠️ A API continuará, mas o banco de dados NÃO funcionará!");
}
else
{
    Console.WriteLine("\n✅ Todas as variáveis de ambiente estão definidas");
}

// Monta a string de conexão
var connectionString = $"Host={dbHost};Port={dbPort};Database={dbName};Username={dbUser};Password={dbPassword};SSL Mode=Require;Trust Server Certificate=true;";

// Log seguro da string (escondendo apenas a senha)
var safeConnectionString = connectionString.Replace(dbPassword ?? "", "***");
Console.WriteLine($"\n🔗 Connection String (segura): {safeConnectionString}");

// Testa se a string é válida
try
{
    var npgsqlConnString = new NpgsqlConnectionStringBuilder(connectionString);
    Console.WriteLine($"✅ String de conexão válida - Host: {npgsqlConnString.Host}, Porta: {npgsqlConnString.Port}, Database: {npgsqlConnString.Database}, SSL: {npgsqlConnString.SslMode}");
}
catch (Exception ex)
{
    Console.WriteLine($"❌ ERRO na string de conexão: {ex.Message}");
}

// ============================================================
// 🔌 REGISTRA DbContext
// ============================================================
builder.Services.AddDbContext<EstudoDbContext>((serviceProvider, options) =>
{
    try
    {
        Console.WriteLine("\n⚙️ Configurando DbContext...");
        
        options.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure(5);
            npgsqlOptions.CommandTimeout(30);
            Console.WriteLine("   - Retry on failure: 5 tentativas");
            Console.WriteLine("   - Command timeout: 30 segundos");
        });
        
        options.UseLazyLoadingProxies();
        Console.WriteLine("   - Lazy loading proxies: ATIVADO");
        
        Console.WriteLine("✅ DbContext configurado com sucesso");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ ERRO na configuração do DbContext: {ex.Message}");
        Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        throw;
    }
});

// ============================================================
// 📝 REGISTRA SERVIÇOS
// ============================================================
builder.Services.AddScoped<IRevisaoService, RevisaoService>();
Console.WriteLine("✅ RevisaoService registrado");

// ============================================================
// 🌐 CORS
// ============================================================
builder.Services.AddCors(options => {
    options.AddPolicy("AllowAll", policy => {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
Console.WriteLine("✅ CORS configurado (AllowAll)");

// ============================================================
// 🏗️ CONSTRÓI A APLICAÇÃO
// ============================================================
Console.WriteLine("\n🏗️ Construindo a aplicação...");
var app = builder.Build();
Console.WriteLine("✅ Aplicação construída");

// ============================================================
// 🔧 PIPELINE DE MIDDLEWARE
// ============================================================
Console.WriteLine("\n🔧 Configurando pipeline...");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    Console.WriteLine("   - Swagger habilitado (Development)");
}
else
{
    Console.WriteLine("   - Swagger desabilitado (Production)");
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();
Console.WriteLine("   - CORS, Authorization e Controllers configurados");

// ============================================================
// 🏥 HEALTH CHECKS
// ============================================================
app.MapGet("/", () => "RevisaFácil API v1.3.2 Online");
app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName,
    database = "checking"
}));
Console.WriteLine("   - Endpoints / e /health configurados");

// ============================================================
// 🗄️ TESTE DE CONEXÃO COM BANCO (DETALHADO)
// ============================================================
Console.WriteLine("\n🔍 TESTANDO CONEXÃO COM POSTGRESQL");
Console.WriteLine("----------------------------------------");

try
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<EstudoDbContext>();
        Console.WriteLine("📡 Obteve DbContext do escopo");
        Console.WriteLine("🔄 Verificando conexão com o banco...");
        
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        Console.WriteLine("⏱️ Timeout configurado: 15 segundos");
        
        try
        {
            Console.WriteLine("📞 Chamando CanConnectAsync...");
            var canConnect = await dbContext.Database.CanConnectAsync(cancellationTokenSource.Token);
            
            if (canConnect)
            {
                Console.WriteLine("✅✅✅ CONEXÃO COM POSTGRESQL BEM-SUCEDIDA! ✅✅✅");
                
                // Tenta obter versão do PostgreSQL
                try
                {
                    var version = await dbContext.Database.SqlQueryRaw<string>("SELECT version()").FirstOrDefaultAsync();
                    Console.WriteLine($"📊 PostgreSQL Version: {version}");
                }
                catch (Exception versionEx)
                {
                    Console.WriteLine($"⚠️ Não foi possível obter versão: {versionEx.Message}");
                }
                
                // Tenta aplicar migrações
                Console.WriteLine("🔄 Verificando migrações pendentes...");
                try
                {
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
                        Console.WriteLine("✅ Migrações aplicadas com sucesso!");
                    }
                    else
                    {
                        Console.WriteLine("✅ Nenhuma migração pendente");
                    }
                }
                catch (Exception migrationEx)
                {
                    Console.WriteLine($"⚠️ Erro ao aplicar migrações: {migrationEx.Message}");
                    if (migrationEx.InnerException != null)
                        Console.WriteLine($"   Inner: {migrationEx.InnerException.Message}");
                }
            }
            else
            {
                Console.WriteLine("❌❌❌ FALHA NA CONEXÃO COM POSTGRESQL ❌❌❌");
                Console.WriteLine("🔧 Verifique os seguintes itens:");
                Console.WriteLine("   1. Host correto? Use pooler.supabase.com");
                Console.WriteLine("   2. Porta correta? 5432 ou 6543");
                Console.WriteLine("   3. Username tem o formato: postgres.xxxxx");
                Console.WriteLine("   4. Senha está correta (sem caracteres especiais)");
                Console.WriteLine("   5. SSL Mode = Require (configurado)");
                Console.WriteLine("   6. Supabase permite conexões externas");
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("❌ TIMEOUT: Conexão excedeu 15 segundos");
            Console.WriteLine("   - Supabase pode estar bloqueando o IP do Render");
            Console.WriteLine("   - Tente usar o pooler: aws-0-us-east-1.pooler.supabase.com");
        }
        catch (PostgresException pgEx)
        {
            Console.WriteLine($"❌ POSTGRES ERROR: {pgEx.SqlState} - {pgEx.MessageText}");
            Console.WriteLine($"   Details: {pgEx.Detail}");
            Console.WriteLine($"   Hint: {pgEx.Hint}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ ERRO DE CONEXÃO DETALHADO:");
            Console.WriteLine($"   Tipo: {ex.GetType().Name}");
            Console.WriteLine($"   Mensagem: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   Inner Type: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"   Inner Message: {ex.InnerException.Message}");
            }
            Console.WriteLine($"   Stack Trace: {ex.StackTrace}");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ ERRO NO ESCOPO DO BANCO: {ex.Message}");
    Console.WriteLine("A API continuará rodando, mas funcionalidades do banco falharão");
}

// ============================================================
// 🚀 FINALIZAÇÃO
// ============================================================
Console.WriteLine("\n" + "=".PadRight(60, '='));
Console.WriteLine($"🚀 API RODANDO - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
Console.WriteLine($"📡 Escutando em: http://localhost:8080");
Console.WriteLine($"🏥 Health check: http://localhost:8080/health");
Console.WriteLine($"📚 Swagger: http://localhost:8080/swagger");
Console.WriteLine("=".PadRight(60, '='));

await app.RunAsync();