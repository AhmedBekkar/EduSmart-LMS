using System.ComponentModel.DataAnnotations;

namespace EduSmart.Models
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }
    }

    public class UserInputViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(120)]
        [Display(Name = "Full name")]
        public string AdSoyad { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Eposta { get; set; }

        [Required]
        [StringLength(60)]
        [Display(Name = "Role")]
        public string Rol { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; }
    }
}


