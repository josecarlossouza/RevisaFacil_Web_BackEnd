// Controllers/DashboardController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Data;
using RevisaFacilApi.Services;

namespace RevisaFacilApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        private readonly IRevisaoService _revisaoService;
        private readonly EstudoDbContext _context;

        public DashboardController(IRevisaoService revisaoService, EstudoDbContext context)
        {
            _revisaoService = revisaoService;
            _context = context;
        }

        [HttpGet("data")]
        public async Task<ActionResult<DashboardData>> GetDashboardData()
        {
            var data = await _revisaoService.GetDashboardData();
            return Ok(data);
        }

        [HttpPost("sincronizar")]
        public async Task<IActionResult> SincronizarTudo()
        {
            await _revisaoService.SincronizarTodasRevisoes();
            return Ok(new { message = "Sincronização concluída" });
        }

        [HttpGet("revisoes/{inicio}/{fim}")]
        public async Task<ActionResult> GetRevisoesPorPeriodo(DateTime inicio, DateTime fim)
        {
            var revisoes = await _revisaoService.GetRevisoesPorPeriodo(inicio, fim);
            return Ok(revisoes);
        }
    }
}