using MatchUp.Utilities.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Players
{
    public class EditPlayerVm
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = default!;

        [Required]
        [StringLength(1000)]
        public string Biography { get; set; } = default!;

        [Required]
        [StringLength(100)]
        public string Nationality { get; set; } = default!;

        [Range(100, 250)]
        public short Height { get; set; }

        [Range(30, 250)]
        public short Weight { get; set; }

        [Required]
        public DateTime? BirthDate { get; set; }

        public string? CurrentImageUrl { get; set; }

        public IFormFile? ImageFile { get; set; }

        [Required]
        public List<PlayerPosition> PlayablePositions { get; set; } = new();

        public List<SelectListItem> NationalityOptions { get; set; } = new();
        public List<SelectListItem> PositionOptions { get; set; } = new();
    }
}
