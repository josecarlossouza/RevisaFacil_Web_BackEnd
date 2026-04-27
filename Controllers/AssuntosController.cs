using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Models;

namespace RevisaFacilApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AssuntosController : ControllerBase
    {
        private readonly EstudoDbContext _context;

        public AssuntosController(EstudoDbContext context)
        {
            _context = context;
        }

        // GET: api/Assuntos
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Assunto>>> GetAssuntos()
        {
            // Usamos o .AsNoTracking() para melhor performance em listagens leitura
            return await _context.Assuntos
                .Include(a => a.Disciplina)
                .OrderByDescending(a => a.DataInicio)
                .ToListAsync();
        }

        // POST: api/Assuntos
        [HttpPost]
        public async Task<ActionResult<Assunto>> PostAssunto(Assunto assunto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 1. Salva o assunto para gerar o ID
            _context.Assuntos.Add(assunto);
            await _context.SaveChangesAsync();

            // 2. Dispara a sincronização de datas e calendário
            await SincronizarRevisoes(assunto);

            return CreatedAtAction(nameof(GetAssuntos), new { id = assunto.Id }, assunto);
        }

        // PUT: api/Assuntos/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAssunto(int id, Assunto assunto)
        {
            if (id != assunto.Id) return BadRequest();

            _context.Entry(assunto).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                // Sempre que editar (mudar data ou intervalos), sincroniza o calendário
                await SincronizarRevisoes(assunto);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Assuntos.Any(e => e.Id == id)) return NotFound();
                throw;
            }

            return NoContent();
        }

        /// <summary>
        /// MOTOR DE SINCRONIZAÇÃO (Lógica v1.3.2)
        /// Recalcula as datas encadeadas e gera as notas para o calendário.
        /// </summary>
        private async Task SincronizarRevisoes(Assunto assunto)
        {
            // 1. Limpar notas de calendário automáticas antigas deste assunto
            // No seu modelo, AssuntoId > 0 indica automático
            var notasAntigas = _context.NotasCalendario.Where(n => n.AssuntoId == assunto.Id);
            _context.NotasCalendario.RemoveRange(notasAntigas);

            // 2. Buscar a configuração global (para intervalos padrão)
            var configGlobal = await _context.Configuracoes.FirstOrDefaultAsync() ?? new Configuracao();

            // 3. Gerar novas notas baseadas na lógica encadeada
            DateTime dataReferencia = assunto.DataInicio;
            int qtdRevisoes = configGlobal.QuantidadeRevisoes;

            for (int i = 1; i <= qtdRevisoes; i++)
            {
                // Pega o intervalo (Int1, Int2...) definido no Assunto
                int intervalo = assunto.GetIntervalo(i);
                
                // Cálculo Encadeado: Data da revisão atual = Data da anterior + intervalo
                DateTime dataRevisao = dataReferencia.AddDays(intervalo);

                _context.NotasCalendario.Add(new NotaCalendario
                {
                    Data = dataRevisao,
                    Conteudo = $"Revisão {i}: {assunto.Titulo}",
                    AssuntoId = assunto.Id
                });

                // A próxima revisão será baseada nesta data (Encadeamento)
                dataReferencia = dataRevisao;
            }

            await _context.SaveChangesAsync();
        }
    }
}