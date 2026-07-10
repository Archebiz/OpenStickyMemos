using Microsoft.EntityFrameworkCore;
using OpenStickyMemos.Api.Models;

namespace OpenStickyMemos.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectInvitation> ProjectInvitations => Set<ProjectInvitation>();
    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── User ──
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(u => u.Email).IsUnique();
            entity.HasIndex(u => new { u.AuthProvider, u.ProviderId }).IsUnique();
        });

        // ── Project ──
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasOne(p => p.Owner)
                  .WithMany(u => u.OwnedProjects)
                  .HasForeignKey(p => p.OwnerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── ProjectMember ──
        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasIndex(pm => new { pm.ProjectId, pm.UserId }).IsUnique();

            entity.HasOne(pm => pm.Project)
                  .WithMany(p => p.Members)
                  .HasForeignKey(pm => pm.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(pm => pm.User)
                  .WithMany(u => u.Memberships)
                  .HasForeignKey(pm => pm.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── RefreshToken ──
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasIndex(rt => rt.Token).IsUnique();

            entity.HasOne(rt => rt.User)
                  .WithMany()
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── ProjectInvitation ──
        modelBuilder.Entity<ProjectInvitation>(entity =>
        {
            entity.HasIndex(i => i.Token).IsUnique();

            entity.HasOne(i => i.Project)
                  .WithMany()
                  .HasForeignKey(i => i.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(i => i.CreatedBy)
                  .WithMany()
                  .HasForeignKey(i => i.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(i => i.AcceptedByUser)
                  .WithMany()
                  .HasForeignKey(i => i.AcceptedByUserId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ── Note ──
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasOne(n => n.Project)
                  .WithMany(p => p.Notes)
                  .HasForeignKey(n => n.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(n => n.Author)
                  .WithMany(u => u.Notes)
                  .HasForeignKey(n => n.AuthorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
