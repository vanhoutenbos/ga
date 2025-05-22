using Microsoft.EntityFrameworkCore;
using GolfApp.Api.Models;

namespace GolfApp.Api.Data
{
    public class GolfAppDbContext : DbContext
    {
        public GolfAppDbContext(DbContextOptions<GolfAppDbContext> options)
            : base(options)
        {
        }

        // Define your entity sets here - these will map to your Supabase tables
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<Player> Players { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Club> Clubs { get; set; }
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Configure entity relationships and map to existing Supabase tables
            modelBuilder.HasDefaultSchema("public");
            
            // Apply Supabase RLS-friendly configurations
            ConfigureTournaments(modelBuilder);
            ConfigurePlayers(modelBuilder);
            ConfigureCourses(modelBuilder);
            ConfigureClubs(modelBuilder);
            
            base.OnModelCreating(modelBuilder);
        }
        
        private void ConfigureTournaments(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tournament>(entity =>
            {
                entity.ToTable("tournaments");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.StartDate).HasColumnName("start_date");
                entity.Property(e => e.EndDate).HasColumnName("end_date");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
                
                // RLS considerations - all EF operations must respect RLS
                // This assumes proper authentication in your API layer
            });
        }
        
        private void ConfigurePlayers(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Player>(entity =>
            {
                entity.ToTable("players");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.FirstName).HasColumnName("first_name").IsRequired();
                entity.Property(e => e.LastName).HasColumnName("last_name").IsRequired();
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.Handicap).HasColumnName("handicap");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            });
        }
        
        private void ConfigureCourses(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Course>(entity =>
            {
                entity.ToTable("courses");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.ClubId).HasColumnName("club_id");
                
                // Relationship with Club
                entity.HasOne(e => e.Club)
                      .WithMany(c => c.Courses)
                      .HasForeignKey(e => e.ClubId);
            });
        }
        
        private void ConfigureClubs(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Club>(entity =>
            {
                entity.ToTable("clubs");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasColumnName("id");
                entity.Property(e => e.Name).HasColumnName("name").IsRequired();
                entity.Property(e => e.Location).HasColumnName("location");
            });
        }
    }
}
