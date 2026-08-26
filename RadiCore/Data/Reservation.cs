using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadiCore.Reservations;

namespace RadiCore.Data
{
    [Table("reservations")]
    public class Reservation
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

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
        public TimeOnly StartTime { get; set; }

        [Column("end_time")]
        public TimeOnly EndTime { get; set; }

        [Column("target_date")]
        public DateOnly? TargetDate { get; set; }

        [Column("repeat_type")]
        public RepeatType RepeatType { get; set; }

        [Column("repeat_days")]
        public DayOfWeek? RepeatDays { get; set; }

        [Column("status")]
        public ReservationStatus Status { get; set; }

        [Column("image_url")]
        public string? ImageUrl { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        [Column("is_manual")]
        public bool? IsManual { get; set; } = false;

        /// <summary>
        /// 繰り返し録音で今回分完了後に前回分を自動削除するか
        /// </summary>
        [Column("auto_delete_previous")]
        public bool AutoDeletePrevious { get; set; } = false;

        /// <summary>「毎週 月 13:00-15:00」のような予約スケジュールの表示文字列</summary>
        [NotMapped]
        public string ScheduleText
        {
            get
            {
                var timeRange = $"{StartTime:HH:mm}-{EndTime:HH:mm}";

                return RepeatType switch
                {
                    RepeatType.Once => TargetDate is not null
                        ? $"{TargetDate:yyyy/MM/dd} {timeRange}"
                        : $"(日付未設定) {timeRange}",

                    RepeatType.Daily => $"毎日 {timeRange}",

                    RepeatType.Weekly => $"毎週 {RepeatDays!.Value.ToJapanese()} {timeRange}",

                    _ => $"不明な予約 {timeRange}"
                };
            }
        }

        public override string ToString()
        {
            return
                $"[Reservation #{Id}] " +
                $"{ScheduleText} / " +
                $"{StationName ?? StationId} / " +
                $"{ProgramName ?? "（番組名不明）"}" +
                (string.IsNullOrWhiteSpace(CastName) ? "" : $" / {CastName}") +
                $" / Status={Status}";
        }
    }
}
