using Microsoft.EntityFrameworkCore;

namespace RadiCore.Data
{
    public class RadiCoreStagingContext : RadiCoreContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Station>().ToTable("stations_staging");
            modelBuilder.Entity<Data.Program>().ToTable("programs_staging");
        }
    }
}
