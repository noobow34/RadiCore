using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadikoShift.Data;
using RadikoShift.Infrastructure;
using RadikoShift.Reservations;

namespace RadikoShift.Controllers
{
    public class ReservationController : Controller
    {
        private readonly ShiftContext _db;
        private readonly QuartzScheduler _scheduler;

        public ReservationController(ShiftContext db, QuartzScheduler scheduler)
        {
            _db        = db;
            _scheduler = scheduler;
        }

        public async Task<IActionResult> Index()
        {
            return View(await GetReservationsAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateReservationRequest req)
        {
            var prg = await _db.Programs.FindAsync(req.ProgramId);
            var sta = await _db.Stations.FindAsync(prg!.StationId);

            var reservation = new Reservation
            {
                ProgramId   = prg!.Id,
                StationId   = prg!.StationId!,
                StationName = sta!.Name,
                ProgramName = req.IsEdited ? req.Title    : prg!.Title,
                CastName    = req.IsEdited ? req.CastName : prg!.CastName,
                ImageUrl    = prg!.ImageUrl,
                StartTime   = req.IsEdited ? req.StartTime : TimeOnly.FromDateTime(prg.StartTime!.Value),
                EndTime     = req.IsEdited ? req.EndTime   : TimeOnly.FromDateTime(prg.EndTime!.Value),
                TargetDate  = req.RepeatType == RepeatType.Once
                              ? DateOnly.FromDateTime(prg.StartTime!.Value)
                              : null,
                RepeatType  = req.RepeatType,
                RepeatDays  = req.RepeatType == RepeatType.Weekly ? prg.StartTime!.Value.DayOfWeek : null,
                Status      = ReservationStatus.Scheduled,
                CreatedAt   = DateTime.Now,
                UpdatedAt   = DateTime.Now,
                IsManual    = req.IsEdited,
                AutoDeletePrevious = req.AutoDeletePrevious
            };

            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync();
            await _scheduler.RegisterAsync(reservation);

            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> Update(UpdateReservationRequest req)
        {
            var reservation = await _db.Reservations.FindAsync(req.Id);
            if (reservation == null) return NotFound();

            reservation.ProgramName = req.Title;
            reservation.CastName    = req.CastName;
            reservation.StartTime   = req.StartTime;
            reservation.EndTime     = req.EndTime;
            reservation.IsManual    = req.IsEdited;
            reservation.AutoDeletePrevious = req.AutoDeletePrevious;
            reservation.UpdatedAt   = DateTime.Now;
            await _db.SaveChangesAsync();

            this.JournalWriteLine($"予約を更新、ジョブを再登録します: {reservation}");
            await _scheduler.UnregisterAsync(reservation);
            await _scheduler.RegisterAsync(reservation);

            return PartialView("_ReservationList", await GetReservationsAsync());
        }

        public async Task<IActionResult> ListPartial()
        {
            return PartialView("_ReservationList", await GetReservationsAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var reservation = await _db.Reservations.FindAsync(id);
            if (reservation == null) return NotFound();

            await _scheduler.UnregisterAsync(reservation);
            reservation.Status    = ReservationStatus.Canceled;
            reservation.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();

            return Ok();
        }

        public async Task<IActionResult> RerunPrevious(int id)
        {
            var r = await _db.Reservations.FindAsync(id);
            if (r == null) return NotFound();

            await _scheduler.RegisterPrevious(r);
            return Ok();
        }

        private async Task<List<Reservation>> GetReservationsAsync()
        {
            return await _db.Reservations
                .Where(r => r.Status == ReservationStatus.Scheduled || r.Status == ReservationStatus.Running)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
        }
    }
}
