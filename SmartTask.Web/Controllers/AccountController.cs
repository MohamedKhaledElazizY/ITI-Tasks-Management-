using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph;
using SmartTask.Core.IRepositories;
using SmartTask.Core.Models;
using SmartTask.Core.Models.AuditModels;
using SmartTask.Core.Models.BasePermission;
using SmartTask.Core.ViewModels;
using System.Data.SqlTypes;
using SmartTask.BL.Services;
using SmartTask.Web.ViewModels;
using SmartTask.BL.IServices;
namespace SmartTask.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> userManager;
        private readonly SignInManager<ApplicationUser> signInManager;
        private readonly RoleManager<ApplicationRole> roleManager;
        private readonly IUserLoginHistoryRepository _userLoginHistory;
        private readonly IDashboardService _dashboardService;
        IConfiguration _config;
        public AccountController(UserManager<ApplicationUser> _userManager,
            SignInManager<ApplicationUser> _signInManager, RoleManager<ApplicationRole> _roleManager,
            IUserLoginHistoryRepository userLoginHistory, IConfiguration config,
            IDashboardService dashboardService)
        {
            userManager = _userManager;
            signInManager = _signInManager;
            roleManager = _roleManager;
            _userLoginHistory = userLoginHistory;
            _config = config;
            _dashboardService = dashboardService;
        }
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel account)
        {
            ApplicationUser user = new ApplicationUser();
            //Console.WriteLine($"{account.UserName}");
            if (ModelState.IsValid)
            {
                try
                {
                    user = await userManager.FindByNameAsync(account.UserName);
                    // ApplicationUser user = await userManager.FindByNameAsync("mohamedali");
                }
                catch (SqlNullValueException ex)
                {
                    Console.WriteLine("Caught null field: " + ex.Message);
                }
                if (user != null)
                {
                    bool result = await userManager.CheckPasswordAsync(user, account.Password);
                    if (result)
                    {
                        await signInManager.SignInAsync(user, isPersistent: account.RememberMe);
                        _userLoginHistory.AddUserLoginHistory(new UserLoginHistory
                        {
                            UserId = user.Id,
                            LoginTime = DateTime.Now,
                            IPAddress = HttpContext.Connection.RemoteIpAddress.ToString(),
                            UserAgent = HttpContext.Request.Headers["User-Agent"].ToString(),
                            UserName = user.UserName,
                          

                        });
                        //DashboardService
                        var preference = await _dashboardService.GetUserDashboardSettingsAsync(user.Id);
                        preference.LastLoginDate = DateTime.Now;
                        await _dashboardService.UpdateUserPreferenceAsync(preference);
                        var roles = await userManager.GetRolesAsync(user);
                        if (!roles.Any()&&account.UserName!="mkelazizy")
                        {
                            return View("pendding");
                        }
                        return RedirectToAction("Index", "Home");
                    }
                }
                ModelState.AddModelError("", "Invalid Email or Password");
            }
            return View("Login", account);
        }

        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel register)
        {
            if (ModelState.IsValid)
            {
                ApplicationUser applicationUser = new ApplicationUser();
                applicationUser.UserName = register.UserName;
                applicationUser.FullName = register.FName;
                applicationUser.Email = register.Email;
                applicationUser.PhoneNumber = register.PhoneNumber;
                applicationUser.createdAt = DateTime.Now;
                applicationUser.updatedAt = DateTime.Now;

                if (register.UserImage != null && register.UserImage.Length > 0)
                {
                    // Check if file size exceeds 1MB (1,048,576 bytes)
                    if (register.UserImage.Length > 1048576)
                    {
                        ModelState.AddModelError("UserImage", "Image size cannot exceed 1MB.");
                        return View("Register", register);
                    }

                    try
                    {
                        // Process the image file
                        var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(register.UserImage.FileName);
                        var imagesFolder = Path.Combine("wwwroot", "assets", "img", "user-img");
                        var imagePath = Path.Combine(Directory.GetCurrentDirectory(), imagesFolder, uniqueFileName);

                        // Ensure the directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(imagePath));

                        // Save the file
                        using (var stream = new FileStream(imagePath, FileMode.Create))
                        {
                            await register.UserImage.CopyToAsync(stream);
                        }

                        // Set the image path to be saved in the database
                        string savedPath = "/assets/img/user-img/" + uniqueFileName;
                        register.ImagePath = savedPath;
                        applicationUser.ImageUrl = savedPath;
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("", $"Error saving image: {ex.Message}");
                        return View("Register", register);
                    }
                }

                IdentityResult identityResult = await userManager.CreateAsync(applicationUser, register.Password);

                if (identityResult.Succeeded)
                {
                    return RedirectToAction("Index", "Home");
                }
                foreach (var item in identityResult.Errors)
                {
                    ModelState.AddModelError("", item.Description);
                }
            }
            return View("Register", register);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromForm] ApplicationUser user, [FromForm] string password, [FromForm] IFormFile userImage)
        {
            // Handle file upload if provided
            if (userImage != null && userImage.Length > 0)
            {
                // Check if file size exceeds 1MB
                if (userImage.Length > 1048576)
                {
                    return BadRequest(new { error = "Image size cannot exceed 1MB." });
                }

                try
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(userImage.FileName);
                    var imagesFolder = Path.Combine("wwwroot", "assets", "img", "user-img");
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), imagesFolder, uniqueFileName);

                    // Ensure the directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(imagePath));

                    using (var stream = new FileStream(imagePath, FileMode.Create))
                    {
                        await userImage.CopyToAsync(stream);
                    }

                    user.ImageUrl = "/assets/img/user-img/" + uniqueFileName;
                }
                catch (Exception ex)
                {
                    return BadRequest(new { error = $"Error saving image: {ex.Message}" });
                }
            }

            // Set creation and update timestamps
            user.createdAt = DateTime.Now;
            user.updatedAt = DateTime.Now;

            // Create user with UserManager
            IdentityResult result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                return CreatedAtAction("Get", "Users", new { id = user.Id }, user);
            }

            return BadRequest(result.Errors);
        }

        

        //[Authorize]
        [HttpGet]
        public async Task<IActionResult> ManageUserRoles(string id)
        {
            List<ApplicationUser> users;
            if (!string.IsNullOrEmpty(id))
            {
                var user = await userManager.FindByIdAsync(id);
                if (user == null)
                    return NotFound();
                users = new List<ApplicationUser> { user };
            }
            else
            {
                users = await userManager.Users.ToListAsync();
            }
            //var users = await userManager.Users.ToListAsync();
            var roles = await roleManager.Roles.Select(r => r.Name).ToListAsync();

            var model = new List<UserRoleAssignmentViewModel>();

            foreach (var user in users)
            {
                var userRoles = await userManager.GetRolesAsync(user);

                model.Add(new UserRoleAssignmentViewModel
                {
                    UserId = user.Id,
                    UserName = user.FullName,
                    Roles = roles.Select(role => new RoleCheckbox
                    {
                        RoleName = role,
                        IsAssigned = userRoles.Contains(role)
                    }).ToList()
                });
            }

            return View(model);
        }

        [HttpPost]

        [Authorize]
        public async Task<IActionResult> ManageUserRoles(List<UserRoleAssignmentViewModel> model, string id)
        {
            foreach (var userModel in model)
            {
                var user = await userManager.FindByIdAsync(userModel.UserId);
                if (user == null) continue;

                var currentRoles = await userManager.GetRolesAsync(user);

                foreach (var role in userModel.Roles)
                {
                    if (role.IsAssigned && !currentRoles.Contains(role.RoleName))
                        await userManager.AddToRoleAsync(user, role.RoleName);

                    if (!role.IsAssigned && currentRoles.Contains(role.RoleName))
                        await userManager.RemoveFromRoleAsync(user, role.RoleName);
                }
            }

            TempData["Message"] = "Roles updated successfully!";
            if (!string.IsNullOrEmpty(id))
                return RedirectToAction("ManageUserRoles", new { id });
            return RedirectToAction("ManageUserRoles");
        }
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await signInManager.SignOutAsync();
            await HttpContext.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
    }
    public class OAuthResponse
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
        public string refresh_token { get; set; }
    }
}