using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiCore.Data
{
    [Table("areas")]
    public class Area
    {
        [Key]
        [Column("area_code")]
        public required string AreaCode { get; set; }

        [Column("area_name")]
        public string? AreaName { get; set; }
    }
}
