using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Teams
{
    public class CreateTeamVm
    {
        [Required]
        [StringLength(150)]
        public string Name { get; set; } = default!;

        [StringLength(1000)]
        public string? Description { get; set; }

        [Required]
        public IFormFile LogoFile { get; set; } = default!;

        [Required]
        public IFormFile ImageFile { get; set; } = default!;

        [Range(1, 99)]
        public byte OwnerSquadNumber { get; set; }
    }
}
