using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadiCore.Data
{
    [Table("stations")]
    public partial class Station
    {
        [Key]
        [Column("id")]
        public string Id { get; set; } = null!;

        [Column("region_id")]
        public string? RegionId { get; set; }

        [Column("region_name")]
        public string? RegionName { get; set; }

        [Column("area")]
        public string? AreaCode { get; set; }

        [Column("name")]
        public string? Name { get; set; }

        [Column("display_order")]
        public int? DisplayOrder { get; set; }

        [ForeignKey("AreaCode")]
        public Area? Area { get; set; }
    }
}
