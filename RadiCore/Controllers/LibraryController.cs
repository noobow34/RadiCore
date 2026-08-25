using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using RadiCore.Data;
using RadiCore.ViewModel;
using System.Data;

namespace RadiCore.Controllers
{
    public class LibraryController : Controller
    {
        private readonly RadiCoreContext _db;

        public LibraryController(RadiCoreContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> IndexAsync()
        {
            var list = await _db.Recordings
                .Include(r => r.Reservation)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new LibraryListViewModel
                {
                    Id              = r.Id,
                    ProgramName     = r.ProgramName,
                    CastName        = r.CastName,
                    StationName     = r.StationName ?? r.StationId,
                    StartTime       = r.StartTime,
                    EndTime         = r.EndTime,
                    FileName        = r.FileName,
                    FileSize        = r.FileSize,
                    ParentReservation = r.Reservation
                })
                .ToListAsync();

            return View(list);
        }

        public async Task<IActionResult> Download(int id)
        {
            await using var conn = (Npgsql.NpgsqlConnection)_db.Database.GetDbConnection();
            await conn.OpenAsync(HttpContext.RequestAborted);

            await using var cmd = new Npgsql.NpgsqlCommand(@"
                select r.file_name, r.mime_type, a.audio_data
                from recordings r
                join recording_audio_data a on a.recording_id = r.id
                where r.id = @id", conn);

            cmd.Parameters.AddWithValue("id", id);

            await using var reader = await cmd.ExecuteReaderAsync(
                CommandBehavior.SequentialAccess, HttpContext.RequestAborted);

            if (!await reader.ReadAsync(HttpContext.RequestAborted))
                return NotFound();

            var fileName = reader.GetString(0);
            var mimeType = reader.IsDBNull(1) ? "audio/mp4" : reader.GetString(1);
            var stream   = reader.GetStream(2);

            Response.ContentType = mimeType;
            var cd = new ContentDispositionHeaderValue("attachment");
            cd.SetHttpFileName(fileName);
            Response.Headers[HeaderNames.ContentDisposition] = cd.ToString();

            await stream.CopyToAsync(Response.Body, 81920, HttpContext.RequestAborted);
            return new EmptyResult();
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var rec = await _db.Recordings.FindAsync(id);
            if (rec == null) return NotFound();

            _db.Recordings.Remove(rec);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
