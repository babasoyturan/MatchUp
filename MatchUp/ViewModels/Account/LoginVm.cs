using System.ComponentModel.DataAnnotations;

namespace MatchUp.ViewModels.Account
{
    public class LoginVm
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = default!;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = default!;
    }
}
