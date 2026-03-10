using MatchUp.Utilities.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Account
{
    public class RegisterVm
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = default!;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;

        [Required]
        [DataType(DataType.Password)]
        [MinLength(6)]
        public string Password { get; set; } = default!;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(Password))]
        public string ConfirmPassword { get; set; } = default!;

        [Required]
        public IFormFile ImageFile { get; set; } = default!;

        [Required]
        [StringLength(1000)]
        public string Biography { get; set; } = default!;

        [Required]
        public string Nationality { get; set; } = default!;

        [Range(100, 250)]
        public short Height { get; set; }

        [Range(30, 200)]
        public short Weight { get; set; }

        [Required]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Required]
        public List<PlayerPosition> PlayablePositions { get; set; } = new();

        public List<SelectListItem> NationalityOptions { get; set; } = new();
        public List<SelectListItem> PositionOptions { get; set; } = new();
    }
}
