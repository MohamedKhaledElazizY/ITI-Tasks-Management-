using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartTask.Bl.IServices;
using SmartTask.Bl.Services;
using SmartTask.BL.IServices;
using SmartTask.Core.Models;
using SmartTask.Web.ViewModels;
using SmartTask.Web.ViewModels.BranchVM;
using System.Linq.Expressions;

namespace SmartTask.Web.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly IUserService _userService;
        private readonly IDepartmentService _departmentService;
        private readonly IBranchService _branchService;
        private readonly UserManager<ApplicationUser> _userManager;
        public UserController(IUserService userService, IDepartmentService departmentService, IBranchService branchService,UserManager<ApplicationUser> uusermanager)
        {
            _userService = userService;
            _departmentService = departmentService;
            _branchService = branchService;
            _userManager = uusermanager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchString = null, int page = 1 , int pageSize = 10)
        {
            var users = await _userService.GetFilteredAsync(searchString, page, pageSize);

            var viewModel = new UsersViewModel
            {
                Users = users,
                SearchString = searchString
            }; 

            return View(viewModel);
        }

        public async Task<IActionResult> WithoutDepartment(int page = 1, int pageSize = 10)
        {

            var users = await _userService.GetUsersWithoutDepartemnt(page, pageSize);

            //var departments = await _departmentService.GetAllDepartmentsAsync();
            //ViewBag.Departments = departments;

            var branches = await _branchService.GetAllAsync();
            ViewBag.Branches = branches;

            var viewModel = new UsersViewModel
            {
                Users = users,
                SearchString = null
            };

            return View("WithoutDepartment", viewModel);
        }

        public async Task<IActionResult> Details(string id)
        {
            if(id == null)
            {
                return BadRequest("User ID cannot be null");
            }

            var user = await _userService.GetByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> AssignToDepartment(string userId, int departmentId)
        {
            var success = await _userService.AssignUserToDepartmentAsync(userId, departmentId);

            if (!success)
            {
                ModelState.AddModelError("", "Failed to assign user to department.");
                return RedirectToAction("WithoutDepartment");
            }
            ViewBag.Departments = await _departmentService.GetAllDepartmentsAsync();

            return RedirectToAction("WithoutDepartment");
        }
        [HttpPost]
        public async Task<IActionResult> AssignToBranch(string userId, int branchId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            user.BranchId = branchId;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "Failed to add user in branch");
            }

            return RedirectToAction("WithoutDepartment");
        }
        [HttpGet]
        public async Task<IActionResult> GetDepartmentsByBranch(int branchId)
        {
            // Get departments that belong to this branch
            var branchDepartments = await _departmentService.GetAllDepartmentsAsync();
            var result = branchDepartments
                .Where(dep => dep.BranchDepartments.Any(bd => bd.BranchId == branchId))
                .Select(dep => new { id = dep.Id, name = dep.Name })
                .ToList();
            return Json(result);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userService.GetByIdAsync(id);
            var viewModel = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                DepartmentId = user.DepartmentId,
                ExistingImagePath = user.ImageUrl,
            };
            var departments = await _departmentService.GetAllDepartmentsAsync();
            var branchs = await _branchService.GetAllAsync();
            ViewBag.Branchs = new SelectList(branchs, "Id", "Name",user.BranchId);
            ViewBag.Departments = new SelectList(departments, "Id", "Name", user.DepartmentId);
            return View(viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        //[Authorize(Policy = "CanEditUser")]
        public async Task<IActionResult> Edit(string id, EditUserViewModel model)
        {
            var departments = await _departmentService.GetAllDepartmentsAsync();
            var branchs = await _branchService.GetAllAsync();
            ViewBag.Departments = new SelectList(departments, "Id", "Name", model.DepartmentId);
            ViewBag.Branchs = new SelectList(branchs, "Id", "Name", model.BranchId);
            if (id != model.Id) return NotFound();
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            var user = await _userService.GetByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.BranchId = model.BranchId;
            user.DepartmentId = model.DepartmentId;
            user.updatedAt = DateTime.UtcNow;

            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(model.ExistingImagePath))
                {
                    var oldImagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", model.ExistingImagePath.TrimStart('/'));
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                var uploadsFolder = Path.Combine("wwwroot", "images", "users");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                user.ImageUrl = $"/images/users/{uniqueFileName}";
            }

            var updateResult = await _userService.UpdateAsync(user);
            if (!updateResult)
            {
                ModelState.AddModelError("", "Failed to update user data.");
                return View(model);
            }

            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                if (string.IsNullOrWhiteSpace(model.CurrentPassword))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is required.");
                    return View(model);
                }

                var passwordCheck = await _userManager.CheckPasswordAsync(user, model.CurrentPassword);
                if (!passwordCheck)
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                    return View(model);
                }

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (!resetResult.Succeeded)
                {
                    foreach (var error in resetResult.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                    return View(model);
                }
            }

            return RedirectToAction(nameof(Index));
        }


        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userService.GetByIdAsync(id);
            if (user == null) return NotFound();

            return View(user);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _userService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> DashBoard(string id)
        {
            await _userService.DeleteAsync(id);
            return RedirectToAction(nameof(Index));
        }

    }
}
