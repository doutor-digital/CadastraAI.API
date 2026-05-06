using CadastraAI.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CadastraAI.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Domain
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Consulta> Consultas => Set<Consulta>();
    public DbSet<Tratamento> Tratamentos => Set<Tratamento>();
    public DbSet<Recebimento> Recebimentos => Set<Recebimento>();

    // Auth + Tenancy
    public DbSet<User> Users => Set<User>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invite> Invites => Set<Invite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Lead → Consulta (1:1)
        modelBuilder.Entity<Lead>()
            .HasOne(l => l.Consulta)
            .WithOne(c => c.Lead)
            .HasForeignKey<Consulta>(c => c.LeadId)
            .OnDelete(DeleteBehavior.Cascade);

        // Consulta → Tratamento (1:1)
        modelBuilder.Entity<Consulta>()
            .HasOne(c => c.Tratamento)
            .WithOne(t => t.Consulta)
            .HasForeignKey<Tratamento>(t => t.ConsultaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Consulta → Recebimentos (1:N — máx 2, validar na aplicação)
        modelBuilder.Entity<Consulta>()
            .HasMany(c => c.Recebimentos)
            .WithOne(r => r.Consulta)
            .HasForeignKey(r => r.ConsultaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tratamento → Recebimentos (1:N — máx 6, validar na aplicação)
        modelBuilder.Entity<Tratamento>()
            .HasMany(t => t.Recebimentos)
            .WithOne(r => r.Tratamento)
            .HasForeignKey(r => r.TratamentoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índices de performance
        modelBuilder.Entity<Lead>()
            .HasIndex(l => l.CreatedAt);
        modelBuilder.Entity<Lead>()
            .HasIndex(l => l.AgendouConsulta);
        modelBuilder.Entity<Lead>()
            .HasIndex(l => l.NomeResponsavel);
        modelBuilder.Entity<Consulta>()
            .HasIndex(c => c.FechouTratamento);

        // ----- Auth / Tenancy -----

        // User: email is unique (case-insensitive at the application layer — we always lowercase)
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.GoogleSubject)
            .IsUnique()
            .HasFilter("\"GoogleSubject\" IS NOT NULL");

        // Empresa → Owner (one user can own many empresas; deletes cascade-restricted to avoid wiping owners by accident).
        modelBuilder.Entity<Empresa>()
            .HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Membership: unique (EmpresaId, UserId)
        modelBuilder.Entity<Membership>()
            .HasIndex(m => new { m.EmpresaId, m.UserId })
            .IsUnique();
        modelBuilder.Entity<Membership>()
            .HasOne(m => m.Empresa)
            .WithMany(e => e.Memberships)
            .HasForeignKey(m => m.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Membership>()
            .HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Invite: unique token; index on email for lookup
        modelBuilder.Entity<Invite>()
            .HasIndex(i => i.Token)
            .IsUnique();
        modelBuilder.Entity<Invite>()
            .HasIndex(i => new { i.EmpresaId, i.Email });
        modelBuilder.Entity<Invite>()
            .HasOne(i => i.Empresa)
            .WithMany(e => e.Invites)
            .HasForeignKey(i => i.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Invite>()
            .HasOne(i => i.InvitedBy)
            .WithMany()
            .HasForeignKey(i => i.InvitedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
