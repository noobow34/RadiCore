using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadiCore.Reservations;

namespace RadiCore.Data
{
    [Table("recordings")]
    public class Recording
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Column("reservation_id")]
        public int ReservationId { get; set; }

        [Column("program_id")]
        [Required]
        public string ProgramId { get; set; } = null!;

        [Column("station_id")]
        [Required]
        public string StationId { get; set; } = null!;

        [Column("station_name")]
        public string? StationName { get; set; }

        [Column("program_name")]
        public string? ProgramName { get; set; }

        [Column("cast_name")]
        public string? CastName { get; set; }

        [Column("start_time")]
        public DateTime StartTime { get; set; }

        [Column("end_time")]
        public DateTime EndTime { get; set; }

        [Column("file_name")]
        [Required]
        public string FileName { get; set; } = null!;

        [Column("mime_type")]
        public string? MimeType { get; set; }

        [Column("file_size")]
        public long FileSize { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [ForeignKey("ReservationId")]
        public Reservation? Reservation { get; set; } = null;

        public override string ToString()
        {
            var timeRange = $"{StartTime:yyyy/MM/dd HH:mm}-{EndTime:HH:mm}";

            string sizeText = FileSize switch
            {
                < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
                < 1024 * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):F1} MB",
                _ => $"{FileSize / (1024.0 * 1024 * 1024):F2} GB"
            };

            return
                $"[Recording #{Id}] " +
                $"{timeRange} / " +
                $"{StationName ?? StationId} / " +
                $"{ProgramName ?? "（番組名不明）"}" +
                (string.IsNullOrWhiteSpace(CastName) ? "" : $" / {CastName}") +
                $" / File={FileName} ({sizeText})" +
                $" / ReservationId={ReservationId}";
        }
    }
}
