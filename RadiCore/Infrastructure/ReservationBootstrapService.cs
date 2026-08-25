using Microsoft.EntityFrameworkCore;
using RadiCore.Data;
using RadiCore.Reservations;

namespace RadiCore.Infrastructure
{
    public class ReservationBootstrapService : IHostedService
    {
        private readonly IServiceProvider _services;

        public ReservationBootstrapService(IServiceProvider services)
        {
            _services = services;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _services.CreateScope();
            var db        = scope.ServiceProvider.GetRequiredService<RadiCoreContext>();
            var scheduler = scope.ServiceProvider.GetRequiredService<QuartzScheduler>();

            var reservations = await db.Reservations
                .Where(r => r.Status == ReservationStatus.Scheduled)
                .ToListAsync(cancellationToken);

            foreach (var r in reservations)
                await scheduler.RegisterAsync(r);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
