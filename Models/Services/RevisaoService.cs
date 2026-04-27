// Services/RevisaoService.cs
using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Models;

namespace RevisaFacilApi.Services
{
    public interface IRevisaoService
    {
        Task<List<NotaCalendario>> SincronizarTodasRevisoes();
        Task<List<NotaCalendario>> GetRevisoesPorPeriodo(DateTime inicio, DateTime fim);
        Task<DashboardData> GetDashboardData();
    }

    public class RevisaoService : IRevisaoService
    {
        private readonly EstudoDbContext _context;

        public RevisaoService(EstudoDbContext context)
        {
            _context = context;
        }

        public async Task<List<NotaCalendario>> SincronizarTodasRevisoes()
        {
            // Limpar todas as notas automáticas
            var notasAutomaticas = await _context.NotasCalendario
                .Where(n => n.AssuntoId != null && n.AssuntoId > 0)
                .ToListAsync();
            
            _context.NotasCalendario.RemoveRange(notasAutomaticas);

            // Buscar todos os assuntos
            var assuntos = await _context.Assuntos
                .Include(a => a.Disciplina)
                .ToListAsync();

            var configGlobal = await _context.Configuracoes.FirstOrDefaultAsync() ?? new Configuracao();

            var novasNotas = new List<NotaCalendario>();

            foreach (var assunto in assuntos)
            {
                // Determinar quantidade de revisões
                int qtdRevisoes = assunto.Disciplina?.QuantidadeRevisoes ?? configGlobal.QuantidadeRevisoes;
                qtdRevisoes = Math.Min(qtdRevisoes, 30);

                DateTime dataReferencia = assunto.DataInicio;

                for (int i = 1; i <= qtdRevisoes; i++)
                {
                    // Pegar intervalo (prioridade: Assunto > Disciplina > Global)
                    int intervalo = assunto.GetIntervalo(i);
                    
                    if (intervalo == 0 && assunto.Disciplina?.GetIntervalo(i) != null)
                        intervalo = assunto.Disciplina.GetIntervalo(i).Value;
                    else if (intervalo == 0)
                        intervalo = configGlobal.GetIntervalo(i);

                    if (intervalo == 0) continue;

                    DateTime dataRevisao = dataReferencia.AddDays(intervalo);

                    novasNotas.Add(new NotaCalendario
                    {
                        Data = dataRevisao,
                        Conteudo = $"🔄 Revisão {i}: {assunto.Titulo}",
                        AssuntoId = assunto.Id
                    });

                    dataReferencia = dataRevisao;
                }
            }

            await _context.NotasCalendario.AddRangeAsync(novasNotas);
            await _context.SaveChangesAsync();

            return novasNotas;
        }

        public async Task<List<NotaCalendario>> GetRevisoesPorPeriodo(DateTime inicio, DateTime fim)
        {
            return await _context.NotasCalendario
                .Where(n => n.Data.Date >= inicio.Date && n.Data.Date <= fim.Date)
                .OrderBy(n => n.Data)
                .ToListAsync();
        }

        public async Task<DashboardData> GetDashboardData()
        {
            var totalDisciplinas = await _context.Disciplinas.CountAsync();
            var totalAssuntos = await _context.Assuntos.CountAsync();
            
            var hoje = DateTime.Today;
            var revisoesHoje = await _context.NotasCalendario
                .CountAsync(n => n.Data.Date == hoje && n.AssuntoId != null && n.AssuntoId > 0);

            var assuntosPorDisciplina = await _context.Disciplinas
                .Select(d => new DisciplinaContagem
                {
                    DisciplinaId = d.Id,
                    DisciplinaNome = d.Nome,
                    Quantidade = d.Assuntos.Count
                })
                .ToListAsync();

            var revisoesProximas = await _context.NotasCalendario
                .Where(n => n.Data.Date >= hoje && n.AssuntoId != null && n.AssuntoId > 0)
                .OrderBy(n => n.Data)
                .Take(10)
                .Select(n => new RevisaoProxima
                {
                    Id = n.Id,
                    Data = n.Data,
                    Conteudo = n.Conteudo,
                    AssuntoId = n.AssuntoId ?? 0
                })
                .ToListAsync();

            return new DashboardData
            {
                TotalDisciplinas = totalDisciplinas,
                TotalAssuntos = totalAssuntos,
                RevisoesHoje = revisoesHoje,
                AssuntosPorDisciplina = assuntosPorDisciplina,
                ProximasRevisoes = revisoesProximas
            };
        }
    }

    public class DashboardData
    {
        public int TotalDisciplinas { get; set; }
        public int TotalAssuntos { get; set; }
        public int RevisoesHoje { get; set; }
        public List<DisciplinaContagem> AssuntosPorDisciplina { get; set; } = new();
        public List<RevisaoProxima> ProximasRevisoes { get; set; } = new();
    }

    public class DisciplinaContagem
    {
        public int DisciplinaId { get; set; }
        public string DisciplinaNome { get; set; } = string.Empty;
        public int Quantidade { get; set; }
    }

    public class RevisaoProxima
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public string Conteudo { get; set; } = string.Empty;
        public int AssuntoId { get; set; }
    }
}