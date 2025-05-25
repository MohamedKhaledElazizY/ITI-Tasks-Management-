using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartTask.Bl.IServices;
using Microsoft.EntityFrameworkCore;
using SmartTask.BL.IServices;
using SmartTask.BL.Services;
using SmartTask.Core.Models;
using SmartTask.Web.ViewModels.ProjectVM;
using SmartTask.DataAccess.Repositories;
using SmartTask.Core.IRepositories;
using ModelTask = SmartTask.Core.Models.Task;

namespace SmartTask.Web.Controllers
{
    //[Authorize]
   
    public class ProjectController : Controller
    {
        private readonly IProjectService _projectService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDepartmentService _departmentService;
        private readonly IBranchService _branchService;
        private readonly ITaskRepository _taskRepository;

        public ProjectController(
            IProjectService projectService,
             IDepartmentService departmentService,
               IBranchService branchService,
               ITaskRepository taskRepository,
            UserManager<ApplicationUser> userManager)
        {
            _projectService = projectService;
            _departmentService = departmentService;
            _branchService = branchService;
            _taskRepository = taskRepository;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string searchString, int? selectedDepartmentId, int? selectedBranchId, int page = 1, int pageSize = 10)
        {
            //var projects = await _projectService.GetFilteredProjectsAsync(searchString, page, pageSize);

            //var viewModel = new ProjectIndexViewModel
            //{
            //    Projects = projects,
            //    SearchString = searchString
            //};

            //return View(viewModel);
            var departments = await _departmentService.GetAllDepartmentsAsync();
            var branches = await _branchService.GetAllAsync();
            int x = 5;
            var projects = await _projectService.GetFilteredByDepartmentProjectsAsync(searchString, selectedDepartmentId, selectedBranchId, page, pageSize);

            var viewModel = new ProjectIndexViewModel
            {
                Projects = projects,
                SearchString = searchString,
                SelectedDepartmentId = selectedDepartmentId,
                SelectedBranchId = selectedBranchId,
                Departments = departments.ToList(),
                Branches = branches.ToList()
            };

            return View(viewModel);
        }
        public async Task<IActionResult> FilterIndex(string searchString, int? selectedDepartmentId, int? selectedBranchId, int page = 1, int pageSize = 10)
        {
            var departments = await _departmentService.GetAllDepartmentsAsync();
            var branches = await _branchService.GetAllAsync();

            var projects = await _projectService.GetFilteredByDepartmentProjectsAsync(searchString, selectedDepartmentId, selectedBranchId, page,pageSize);

            var viewModel = new ProjectIndexViewModel
            {
                Projects = projects,
                SearchString = searchString,
                SelectedDepartmentId = selectedDepartmentId,
                SelectedBranchId = selectedBranchId,
                Departments = departments.ToList(),
                Branches = branches.ToList()
            };

            return View(viewModel);
          
        }

        [HttpGet]
        public async Task<ActionResult> ProjectTasksGantt(int projectId)
        
        {
            ViewBag.Id = projectId;
            var project = await _projectService.GetProjectByIdAsync(projectId);
            ViewBag.ProjectName = project.Name;
            return View();
        }
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            ViewBag.AdminUsers = new SelectList(admins, "Id", "FullName");
            ViewBag.departments = await _departmentService.GetAllDepartmentsAsync();
            ViewBag.branches = await _branchService.GetAllAsync();
         

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(ProjectFormViewModel model)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            ViewBag.AdminUsers = new SelectList(admins, "Id", "FullName");
            ViewBag.departments = await _departmentService.GetAllDepartmentsAsync();
            ViewBag.branches = await _branchService.GetAllAsync();

            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date must be after start date.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);

            var project = new Project
            {
                Name = model.Name,
                Description = model.Description,
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                OwnerId = model.OwnerId,
                CreatedById = currentUser.Id,
                DepartmentId = model.SelectedDepartmentId,
                BranchId = model.SelectedBranchId
            };

            await _projectService.AddProjectAsync(project);
            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null)
            {
                return View("NotFound");
            }

            await _projectService.DeleteProjectAsync(id);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var project = await _projectService.GetProjectByIdAsync(id);
            if (project == null)
            {
                return NotFound();
            }
            return View(project);
        }


        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var project = await _projectService.GetProjectByIdAsync(id);
            ViewBag.departments = await _departmentService.GetAllDepartmentsAsync();
            ViewBag.branches = await _branchService.GetAllAsync();
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            ViewBag.AdminUsers = new SelectList(admins, "Id", "FullName");

            if (project == null)
            {
                return NotFound();
            }

            var currentUserIds = project.ProjectMembers.Select(pm => pm.UserId).ToList();

            var allUsers = await _userManager.Users.ToListAsync();
            var nonAssignedUsers = allUsers.Where(u => !currentUserIds.Contains(u.Id)).ToList();
           
            ViewBag.NonAssignedUsers = new SelectList(nonAssignedUsers, "Id", "FullName");

            var model = new ProjectEditViewModel
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                OwnerId = project.OwnerId,
                Status = project.Status,
                SelectedDepartmentId = project.DepartmentId,
                SelectedBranchId = project.BranchId,
                AssignedUsers = project.ProjectMembers.Select(pm => new UserCheckboxModel
                {
                    UserId = pm.UserId,
                    FullName = pm.User.FullName,
                    IsChecked = true
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(ProjectEditViewModel model, List<string> AssignedUserIds)
        {
            ViewBag.departments = await _departmentService.GetAllDepartmentsAsync();
            ViewBag.branches = await _branchService.GetAllAsync();
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date must be after start date.");
            }

            if (!ModelState.IsValid)
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                ViewBag.AdminUsers = new SelectList(admins, "Id", "FullName");
                return View(model);
            }

            var project = await _projectService.GetProjectByIdAsync(model.Id);
            if (project == null)
            {
                return NotFound();
            }

            project.Name = model.Name;
            project.Description = model.Description;
            project.StartDate = model.StartDate;
            project.EndDate = model.EndDate;
            project.OwnerId = model.OwnerId;
            project.Status = model.Status;
            project.DepartmentId = model.SelectedDepartmentId;
            project.BranchId = model.SelectedBranchId;

            var updatedMembers = AssignedUserIds.Select(userId => new ProjectMember
            {
                ProjectId = project.Id,
                UserId = userId
            }).ToList();

            project.ProjectMembers.Clear();
            foreach (var member in updatedMembers)
            {
                project.ProjectMembers.Add(member);
            }

            await _projectService.UpdateProjectAsync(project);
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> AssignUser(int projectId)
        {
            var project = await _projectService.GetProjectByIdAsync(projectId);
            if (project == null) return NotFound();

            var projectMemberIds = project.ProjectMembers.Select(pm => pm.UserId).ToList();

            var users = await _userManager.Users
                .Where(u => !projectMemberIds.Contains(u.Id))
                .ToListAsync();

            ViewBag.ProjectId = projectId;
            ViewBag.Users = new SelectList(users, "Id", "FullName");

            return View(projectId);
        }

        [HttpPost]
        public async Task<IActionResult> AssignUser(int projectId, List<string> selectedUserIds)
        {
            foreach (var userId in selectedUserIds)
            {
                await _projectService.AddMemberAsync(projectId, userId);
            }

            return RedirectToAction("Index");
        }


        #region Gantt Chart Data
        public class TaskModel
        {
            public int Id { get; set; }
            public string Text { get; set; }
            public DateTime StartDate { get; set; }
            public int Duration { get; set; }
            public double Progress { get; set; }
            public string Assignee { get; set; }
            public string Status { get; set; }
            public int? Parent { get; set; }
        }

        [HttpGet]
        public IActionResult GetTasks(int id = 1)
        {
            var tasks = _taskRepository.GetByProjectIdAsync(id).Result.Select(task => new TaskModel
            {
                Id = task.Id,
                Text = task.Title,
                StartDate = task.StartDate ?? DateTime.MinValue,
                Duration = task.EndDate.HasValue && task.StartDate.HasValue
                    ? (int)(task.EndDate.Value - task.StartDate.Value).TotalDays
                    : 0,
                Progress = 0.0, // Default value, adjust as needed
                Assignee = task.AssignedTo?.FullName ?? "Unassigned",
                Status = task.Status,
                Parent = task.ParentTaskId
            }).ToList();

            var links = _taskRepository.GetByProjectIdAsync(id).Result.Select(l => new
            {
                id = l.Id,
                source = l.ParentTaskId,
                target = l.Id,
                type = "0" // Finish-to-Start
            }).ToList();

            var data = new
            {
                data = tasks,
                links = links
            };

            return Json(data);
        }

        
        [HttpPost("SaveTask")]
        [IgnoreAntiforgeryToken]
       
        public async Task<IActionResult> SaveTask()
        {
            try
            {
                var formData = Request.Form;
                Console.WriteLine("Form Data: " + string.Join(", ", formData.Select(f => $"{f.Key}: {f.Value}")));

                var action = formData["!nativeeditor_status"];
                var id = int.Parse(formData["id"]);
                var text = formData["text"];
                var startDate = DateTime.Parse(formData["start_date"]);
                var duration = int.Parse(formData["duration"]);
                var parentStr = formData["parent"];
                var assignee = formData["assignee"];
                var status = formData["status"];

                int? parent = string.IsNullOrEmpty(parentStr) || parentStr == "0" ? null : int.Parse(parentStr);

                if (action == "inserted")
                {
                    var newTask = new ModelTask
                    {
                        Title = text,
                        StartDate = startDate,
                        EndDate = startDate.AddDays(duration),
                        ParentTaskId = parent,
                        AssignedToId = assignee,
                        Status = status,
                        ProjectId = 1
                    };

                    await _taskRepository.AddAsync(newTask);
                    return Json(new { action = "inserted", tid = newTask.Id });
                }
                else if (action == "updated")
                {
                    var existing = await _taskRepository.GetByIdAsync(id);
                    if (existing != null)
                    {
                        existing.Title = text;
                        existing.StartDate = startDate;
                        existing.EndDate = startDate.AddDays(duration);
                        existing.ParentTaskId = parent;
                        existing.AssignedToId = assignee;
                        existing.Status = status;
                        await _taskRepository.UpdateAsync(existing);
                    }
                    return Json(new { action = "updated" });
                }

                return StatusCode(400, new { error = "Invalid action" });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error in SaveTask: " + ex.Message);
                return StatusCode(500, new { error = ex.Message });
            }
        }
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> DeleteTask()
        {
            var id = int.Parse(Request.Form["id"]);

            await _taskRepository.DeleteAsync(id);

            return Json(new { action = "deleted" });
        }


        //public ActionResult TaskDetails(int id)
        //{
           
        //    var task = _taskRepository.GetWithDetailsAsync(id);
        //    return PartialView("_DetailsPartial", task);
        //}
    }
        #endregion

    
}
