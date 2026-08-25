using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiCore.Data
{
    [Table("recording_audio_data")]
    public class RecordingAudioData
    {
        [Key]
        [Column("recording_id")]
        public int RecordingId { get; set; }

        [Column("audio_data")]
        public byte[] AudioData { get; set; } = default!;
    }
}
