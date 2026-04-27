using Microsoft.EntityFrameworkCore;
using RevisaFacilApi.Models;

namespace RevisaFacilApi.Data
{
    public class EstudoDbContext : DbContext
    {
        // O construtor agora recebe as opções configuradas no Program.cs
        public EstudoDbContext(DbContextOptions<EstudoDbContext> options) : base(options)
        {
        }

        public DbSet<Assunto> Assuntos { get; set; }
        public DbSet<Disciplina> Disciplinas { get; set; }
        public DbSet<Configuracao> Configuracoes { get; set; }
        public DbSet<NotaCalendario> NotasCalendario { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Ativa o Lazy Loading Proxies (importante para manter a lógica do seu app original)
            optionsBuilder.UseLazyLoadingProxies();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Mantém o relacionamento Disciplina -> Assuntos
            modelBuilder.Entity<Assunto>()
                .HasOne(a => a.Disciplina)
                .WithMany(d => d.Assuntos)
                .HasForeignKey(a => a.DisciplinaId);

            base.OnModelCreating(modelBuilder);
        }
    }
}