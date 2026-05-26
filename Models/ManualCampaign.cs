using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Synctool.Models
{
    public class ManualCampaign
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public string Category { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation property
        public virtual ICollection<ManualCampaignProduct> Products { get; set; } = new List<ManualCampaignProduct>();
    }

    public class ManualCampaignProduct
    {
        [Key]
        public int Id { get; set; }
        
        public int ManualCampaignId { get; set; }
        public virtual ManualCampaign ManualCampaign { get; set; } = null!;
        
        [Required]
        public string ProductCode { get; set; } = string.Empty;
    }
}
