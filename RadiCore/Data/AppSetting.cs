using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiCore.Data
{
    [Table("app_settings")]
    public class AppSetting
    {
        [Key]
        [Column("key")]
        public string Key { get; set; } = null!;

        [Column("value")]
        public string Value { get; set; } = null!;
    }
}
