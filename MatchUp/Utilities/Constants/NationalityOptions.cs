using Microsoft.AspNetCore.Mvc.Rendering;

namespace MatchUp.Utilities.Constants
{
    public static class NationalityOptions
    {
        public static List<SelectListItem> All => new()
        {
            new() { Value = "Azerbaijan", Text = "Azerbaijan" },
            new() { Value = "Turkey", Text = "Turkey" },
            new() { Value = "Colombia", Text = "Colombia" },
            new() { Value = "Brazil", Text = "Brazil" },
            new() { Value = "Argentina", Text = "Argentina" },
            new() { Value = "Portugal", Text = "Portugal" },
            new() { Value = "Spain", Text = "Spain" },
            new() { Value = "France", Text = "France" },
            new() { Value = "Germany", Text = "Germany" },
            new() { Value = "Italy", Text = "Italy" },
            new() { Value = "Netherlands", Text = "Netherlands" },
            new() { Value = "Belgium", Text = "Belgium" },
            new() { Value = "Croatia", Text = "Croatia" },
            new() { Value = "Serbia", Text = "Serbia" },
            new() { Value = "England", Text = "England" }
        };
    }
}
