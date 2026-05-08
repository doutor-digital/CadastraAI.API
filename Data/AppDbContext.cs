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
    public DbSet<MotivoNaoFechamento> MotivosNaoFechamento => Set<MotivoNaoFechamento>();
    public DbSet<DashboardSnapshot> DashboardSnapshots => Set<DashboardSnapshot>();

    // Auditoria
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Integrações
    public DbSet<KommoIntegration> KommoIntegrations => Set<KommoIntegration>();
    public DbSet<KommoInboxItem> KommoInboxItems => Set<KommoInboxItem>();

    // Auth + Tenancy
    public DbSet<User> Users => Set<User>();
    public DbSet<Empresa> Empresas => Set<Empresa>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Invite> Invites => Set<Invite>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Lead → Empresa (multi-tenant scoping)
        modelBuilder.Entity<Lead>()
            .HasOne(l => l.Empresa)
            .WithMany()
            .HasForeignKey(l => l.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);

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
        modelBuilder.Entity<Lead>()
            .HasIndex(l => new { l.EmpresaId, l.CreatedAt });
        modelBuilder.Entity<Lead>()
            .HasIndex(l => new { l.EmpresaId, l.Importado });
        modelBuilder.Entity<Consulta>()
            .HasIndex(c => c.FechouTratamento);

        // Motivos de não fechamento por empresa (nome único dentro da empresa)
        modelBuilder.Entity<MotivoNaoFechamento>()
            .HasIndex(m => new { m.EmpresaId, m.Nome })
            .IsUnique();
        modelBuilder.Entity<MotivoNaoFechamento>()
            .HasOne(m => m.Empresa)
            .WithMany()
            .HasForeignKey(m => m.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);

        // Dashboard snapshots — listagem por empresa ordenada por data de captura.
        modelBuilder.Entity<DashboardSnapshot>()
            .HasIndex(s => new { s.EmpresaId, s.CapturedAt });
        modelBuilder.Entity<DashboardSnapshot>()
            .HasOne(s => s.Empresa)
            .WithMany()
            .HasForeignKey(s => s.EmpresaId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DashboardSnapshot>()
            .HasOne(s => s.CapturedBy)
            .WithMany()
            .HasForeignKey(s => s.CapturedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

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

        // ----- Audit log + CreatedBy -----

        // Restrict pra não cascatear delete de User → AuditLog (queremos manter histórico).
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<AuditLog>()
            .HasOne(a => a.Empresa).WithMany().HasForeignKey(a => a.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.EmpresaId, a.At });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.EmpresaId, a.Action });
        modelBuilder.Entity<AuditLog>().HasIndex(a => new { a.EmpresaId, a.UserId });

        // CreatedByUserId nas entidades de domínio: SetNull no delete de User (preserva o registro
        // mas zera a referência), e Restrict não funciona porque o Owner do empresa pode ser quem criou.
        modelBuilder.Entity<Lead>()
            .HasOne(l => l.CreatedBy).WithMany().HasForeignKey(l => l.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Consulta>()
            .HasOne(c => c.CreatedBy).WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Tratamento>()
            .HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<Recebimento>()
            .HasOne(r => r.CreatedBy).WithMany().HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Lead>().HasIndex(l => l.CreatedByUserId);
        modelBuilder.Entity<Consulta>().HasIndex(c => c.CreatedByUserId);
        modelBuilder.Entity<Tratamento>().HasIndex(t => t.CreatedByUserId);
        modelBuilder.Entity<Recebimento>().HasIndex(r => r.CreatedByUserId);

        // ----- Kommo integration -----

        modelBuilder.Entity<KommoIntegration>()
            .HasIndex(k => k.EmpresaId).IsUnique();
        modelBuilder.Entity<KommoIntegration>()
            .HasOne(k => k.Empresa).WithMany().HasForeignKey(k => k.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<KommoIntegration>()
            .HasOne(k => k.CreatedBy).WithMany().HasForeignKey(k => k.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<KommoInboxItem>()
            .HasOne(i => i.Empresa).WithMany().HasForeignKey(i => i.EmpresaId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<KommoInboxItem>()
            .HasOne(i => i.ImportedLead).WithMany().HasForeignKey(i => i.ImportedLeadId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<KommoInboxItem>()
            .HasOne(i => i.ImportedBy).WithMany().HasForeignKey(i => i.ImportedByUserId).OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<KommoInboxItem>()
            .HasIndex(i => new { i.EmpresaId, i.Status, i.ReceivedAt });
        modelBuilder.Entity<KommoInboxItem>()
            .HasIndex(i => new { i.EmpresaId, i.KommoLeadId });
    }
}
