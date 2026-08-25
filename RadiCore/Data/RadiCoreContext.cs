using Microsoft.EntityFrameworkCore;

namespace RadiCore.Data
{
    public partial class RadiCoreContext : DbContext
    {
        public RadiCoreContext() => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        public RadiCoreContext(DbContextOptions<RadiCoreContext> options) : base(options)
            => AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        public virtual DbSet<Program> Programs { get; set; }
        public virtual DbSet<Station> Stations { get; set; }
        public virtual DbSet<Area> Areas { get; set; }
        public virtual DbSet<Reservation> Reservations { get; set; }
        public virtual DbSet<Recording> Recordings { get; set; }
        public virtual DbSet<RecordingAudioData> RecordingAudioData { get; set; }
        public virtual DbSet<AppSetting> AppSettings { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            string connectionString = Environment.GetEnvironmentVariable("RADICORE_CONNECTION_STRING") ?? "";
            optionsBuilder.UseNpgsql(connectionString);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Station>(entity =>
            {
                entity.HasKey(e => e.Id).HasName("stations_pkey");
            });

            modelBuilder.Entity<Reservation>()
                .Property(r => r.StartTime)
                .HasColumnType("time");

            modelBuilder.Entity<Reservation>()
                .Property(r => r.EndTime)
                .HasColumnType("time");

            modelBuilder.Entity<Reservation>()
                .Property(r => r.TargetDate)
                .HasColumnType("date");

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
