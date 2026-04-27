// Controllers/NotasCalendarioController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Models;

namespace RevisaFacilApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NotasCalendarioController : ControllerBase
    {
        private readonly EstudoDbContext _context;

        public NotasCalendarioController(EstudoDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<NotaCalendario>>> GetNotas(
            [FromQuery] DateTime? inicio, 
            [FromQuery] DateTime? fim)
        {
            var query = _context.NotasCalendario.AsQueryable();

            if (inicio.HasValue)
                query = query.Where(n => n.Data.Date >= inicio.Value.Date);
            
            if (fim.HasValue)
                query = query.Where(n => n.Data.Date <= fim.Value.Date);

            return await query.OrderBy(n => n.Data).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<NotaCalendario>> GetNota(int id)
        {
            var nota = await _context.NotasCalendario.FindAsync(id);
            if (nota == null)
                return NotFound();
            return nota;
        }

        [HttpPost]
        public async Task<ActionResult<NotaCalendario>> PostNota(NotaCalendario nota)
        {
            nota.AssuntoId = -2; // Marcar como manual
            _context.NotasCalendario.Add(nota);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetNota), new { id = nota.Id }, nota);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutNota(int id, NotaCalendario nota)
        {
            if (id != nota.Id)
                return BadRequest();

            _context.Entry(nota).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteNota(int id)
        {
            var nota = await _context.NotasCalendario.FindAsync(id);
            if (nota == null)
                return NotFound();

            _context.NotasCalendario.Remove(nota);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}