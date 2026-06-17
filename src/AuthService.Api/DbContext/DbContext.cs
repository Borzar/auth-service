using Microsoft.EntityFrameworkCore;
using AuthService.Api.Model;

namespace AuthService.AuthDbContext;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // table user
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id)
                .HasColumnName("id");

            entity.Property(e => e.Username)
                .HasColumnName("username");

            entity.Property(e => e.Email)
                .HasColumnName("email");

            entity.Property(e => e.PasswordHash)
                .HasColumnName("password_hash");

            entity.Property(e => e.Role)
                .HasColumnName("role");

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at");
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_token");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.UserId)
                .HasColumnName("user_id");

            entity.Property(x => x.Token)
                .HasColumnName("token");

            entity.Property(x => x.ExpiresAt)
                .HasColumnName("expires_at");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at");
        });
    }

}