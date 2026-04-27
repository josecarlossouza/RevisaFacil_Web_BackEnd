// DisciplinasController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Models;

namespace RevisaFacilApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DisciplinasController : ControllerBase
    {
        private readonly EstudoDbContext _context;

        public DisciplinasController(EstudoDbContext context)
        {
            _context = context;
        }

        // GET: api/Disciplinas
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Disciplina>>> GetDisciplinas()
        {
            return await _context.Disciplinas
                .OrderBy(d => d.Nome)
                .ToListAsync();
        }

        // GET: api/Disciplinas/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Disciplina>> GetDisciplina(int id)
        {
            var disciplina = await _context.Disciplinas
                .Include(d => d.Assuntos)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (disciplina == null)
                return NotFound();

            return disciplina;
        }

        // POST: api/Disciplinas
        [HttpPost]
        public async Task<ActionResult<Disciplina>> PostDisciplina(Disciplina disciplina)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.Disciplinas.Add(disciplina);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetDisciplina), new { id = disciplina.Id }, disciplina);
        }

        // PUT: api/Disciplinas/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDisciplina(int id, Disciplina disciplina)
        {
            if (id != disciplina.Id)
                return BadRequest();

            _context.Entry(disciplina).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Disciplinas.Any(e => e.Id == id))
                    return NotFound();
                throw;
            }

            return NoContent();
        }

        // DELETE: api/Disciplinas/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDisciplina(int id)
        {
            var disciplina = await _context.Disciplinas.FindAsync(id);
            if (disciplina == null)
                return NotFound();

            _context.Disciplinas.Remove(disciplina);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}