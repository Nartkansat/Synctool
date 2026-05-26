using System;

namespace Synctool.Models
{
    public class UserGuide
    {
        public int Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
