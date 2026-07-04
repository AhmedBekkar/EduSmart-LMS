using System;

namespace EduSmart.Models
{
    public class Notification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        // If null, it's a global notification or role-based
        public int? UserId { get; set; }
        
        // e.g. "Ogrenci", "Ogretmen", "Yonetici", or "All"
        public string Role { get; set; }
        
        public string Title { get; set; }
        public string Message { get; set; }
        
        // CSS class for the icon (e.g., "fa-solid fa-star text-warning")
        public string Icon { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
    }
}
