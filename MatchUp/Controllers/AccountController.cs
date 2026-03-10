using MatchUp.Models.Concretes;
using MatchUp.Services.Abstracts;
using MatchUp.Utilities.Constants;
using MatchUp.Utilities.Enums;
using MatchUp.ViewModels.Account;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace MatchUp.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<Player> _userManager;
        private readonly SignInManager<Player> _signInManager;
        private readonly ICloudinaryService _cloudinaryService;

        public AccountController(
            UserManager<Player> userManager,
            SignInManager<Player> signInManager,
            ICloudinaryService cloudinaryService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _cloudinaryService = cloudinaryService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            var vm = new RegisterVm
            {
                NationalityOptions = GetNationalityOptions(),
                PositionOptions = GetPositionOptions()
            };

            return View(vm);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterVm vm)
        {
            vm.NationalityOptions = GetNationalityOptions();
            vm.PositionOptions = GetPositionOptions();

            if (!ModelState.IsValid)
                return View(vm);

            if (vm.PlayablePositions is null || vm.PlayablePositions.Count == 0)
            {
                ModelState.AddModelError(nameof(vm.PlayablePositions), "At least one playable position must be selected.");
                return View(vm);
            }

            if (vm.ImageFile is null || vm.ImageFile.Length == 0)
            {
                ModelState.AddModelError(nameof(vm.ImageFile), "Image is required.");
                return View(vm);
            }

            var existingUserByEmail = await _userManager.FindByEmailAsync(vm.Email);
            if (existingUserByEmail is not null)
            {
                ModelState.AddModelError(nameof(vm.Email), "This email is already in use.");
                return View(vm);
            }

            var uploadResult = await _cloudinaryService.UploadImageAsync(vm.ImageFile);

            if (uploadResult is null || string.IsNullOrWhiteSpace(uploadResult.Url))
            {
                ModelState.AddModelError(nameof(vm.ImageFile), "Image upload failed.");
                return View(vm);
            }

            var player = new Player
            {
                Id = Guid.NewGuid(),
                UserName = vm.Email,
                Email = vm.Email,
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                ImageUrl = uploadResult.Url,
                Biography = vm.Biography,
                Nationality = vm.Nationality,
                Height = vm.Height,
                Weight = vm.Weight,
                BirthDate = vm.BirthDate!.Value,
                PlayablePositions = vm.PlayablePositions,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(player, vm.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return View(vm);
            }

            var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
            var roleToAssign = admins.Count == 0 ? AppRoles.Admin : AppRoles.User;

            var roleResult = await _userManager.AddToRoleAsync(player, roleToAssign);

            if (!roleResult.Succeeded)
            {
                foreach (var error in roleResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                await _userManager.DeleteAsync(player);
                return View(vm);
            }

            await _signInManager.SignInAsync(player, isPersistent: false);

            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVm vm, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;

            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.FindByEmailAsync(vm.Email);

            if (user is null)
            {
                ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
                return View(vm);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user,
                vm.Password,
                isPersistent: true,
                lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
                return View(vm);
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private static List<SelectListItem> GetNationalityOptions()
        {
            return NationalityOptions.All;
        }

        private static List<SelectListItem> GetPositionOptions()
        {
            return Enum.GetValues<PlayerPosition>()
                .Select(x => new SelectListItem
                {
                    Value = ((int)x).ToString(),
                    Text = x.ToString()
                })
                .ToList();
        }
    }
}
