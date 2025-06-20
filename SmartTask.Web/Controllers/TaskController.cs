﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Models;
using SmartTask.BL.IServices;
using SmartTask.BL.IServices;
using SmartTask.BL.Service.Hubs;
using SmartTask.BL.Services;
using SmartTask.Core.IRepositories;
using SmartTask.Core.Models;
using SmartTask.Core.Models.Enums;
using SmartTask.Core.Models.Notification;
using SmartTask.Core.ViewModels;
using SmartTask.DataAccess.Data;
using SmartTask.Web.Models;

//using SmartTask.Web.Models;
using SmartTask.Web.ViewModels;
using SmartTask.Web.ViewModels.KanbanVM;
using System.ComponentModel;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using TaskModel = SmartTask.Core.Models.Task;

namespace SmartTask.Web.Controllers
{
    [Authorize]
    [DisplayName("Task")]
    public class TaskController : Controller
    {
        private readonly SmartTaskContext _context;
        private readonly ITaskRepository _taskRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAssignTaskRepository _assignTaskRepository;
        private readonly INotificationRepository _notificationRepository;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ITaskService _taskService;
        private readonly IWebHostEnvironment _environment;
        private readonly IUserColumnPreferenceService _userColumnPreferenceService;
        private readonly ITaskDependencyRepository _taskDependencyRepo;
        private readonly INotificationService _notificationService;
        private readonly IEventRepository eventRepository;
        public TaskController(ITaskRepository taskRepository, IProjectRepository projectRepository,
            UserManager<ApplicationUser> usermanager, SmartTaskContext context,
            IAssignTaskRepository assignTaskRepository, INotificationRepository notificationRepository,
            IHubContext<NotificationHub> hub, ITaskService taskService
            , IWebHostEnvironment environment, INotificationService notificationService
            , IUserColumnPreferenceService userColumnPreferenceService, ITaskDependencyRepository taskDependencyRepo
            , IEventRepository eventRepository)
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _userManager = usermanager;
            _context = context;
            _assignTaskRepository = assignTaskRepository;
            _notificationRepository = notificationRepository;
            _hub = hub;
            _taskService = taskService;
            _environment = environment;
            _userColumnPreferenceService = userColumnPreferenceService;
            _notificationService = notificationService;
            _taskDependencyRepo = taskDependencyRepo;
            this.eventRepository = eventRepository;
        }
        [DisplayName("Task Details")]
        public async Task<IActionResult> Details(int id)
        {
            var task = await _taskService.Details(id);
            if (task == null)
            {
                return NotFound();
            }
            return PartialView("_DetailsPartial", task);
        }
        [DisplayName("Add Comment")]
        [HttpPost]
        public async Task<IActionResult> AddComment(int taskId, string authorId, string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("Comment content is required");
            }
            Comment comment = await _taskService.AddComment(taskId, authorId, content);
            if (comment == null)
            {
                return StatusCode(500, "Failed to add comment");
            }

            IEnumerable<AssignTask> taskUsers = await _assignTaskRepository.GetByTaskIdAsync(comment.TaskId);

            //SignalR Part

            var user = await _userManager.GetUserAsync(User);
            var users = _assignTaskRepository.GetUsersIdByTaskId(comment.TaskId);
            string notificationMessage = $"{user.FullName} Commented on : {comment.Task.Title}";
            string notificationType = "comment";
            _notificationService.sendSignalRNotificationAsync(users, user.Id, notificationType, notificationMessage, taskId);
            

            return Json(new
            {
                author = comment.Author?.FullName,
                content = comment.Content,
                createdAt = comment.CreatedAt.ToString("dd-MM-yyyy HH:mm")
            });

            //return Ok();
        }

        [DisplayName("Add Attachment")]
        [HttpPost]
        public async Task<IActionResult> AddAttachment(int taskId, IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file selected");
            }

            var user = await _userManager.GetUserAsync(User);

            var attachment = await _taskService.AddAttachment(taskId, file, user.Id, _environment.WebRootPath);

            if (attachment == null)
            {
                return StatusCode(500, "Failed to upload attachment");
            }

            //SignalR Part

            var users = _assignTaskRepository.GetUsersIdByTaskId(attachment.TaskId);
            string notificationMessage = $"{user.FullName} Added Attachment on : {attachment.Task.Title}";
            string notificationType = "attachment";
            _notificationService.sendSignalRNotificationAsync(users, user.Id, notificationType, notificationMessage, taskId);
            

            return Json(new
            {
                fileName = attachment.FileName,
                filePath = attachment.FilePath,
                createdAt = attachment.CreatedAt.ToString("dd-MM-yyyy HH:mm")
            });
        }
        [DisplayName("View Assigned Tasks Only In Project")]
        public async Task<IActionResult> TasksForUserInProject(int projectId , bool isPartial = false)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tasks = await _taskService.TasksForUserInProject(projectId, userId);
            if (isPartial)
            {
                return PartialView("Tasks", tasks);
            }
            return View("Tasks", tasks);
        }
        [DisplayName("View All Tasks In Project")]
        public async Task<IActionResult> TasksForProject(int projectId, bool isPartial = false)
        {
            var tasks = await _taskService.TasksForProject(projectId);
            if (isPartial)
            {
                return PartialView("Tasks", tasks);
            }
            return View("Tasks", tasks);
        }
        [DisplayName("View All Tasks Assigned To User")]
        public async Task<IActionResult> TasksForUser(bool isPartial = false)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var tasks = await _taskService.TasksForUser(userId);
            if (isPartial)
            {
                return PartialView("Tasks", tasks);
            }
            return View("Tasks", tasks);
        }
        [DisplayName("Know the Num Of Dependencies On A Task")]
        [HttpGet]
        public async Task<int> Depend(int taskid)
        {
            return await _taskService.NumofDepend(taskid);
        }
        [DisplayName("Delete Task")]
        [HttpDelete]
        public async Task<IActionResult> DeleteTask(int taskid)
        {
            var task = await _taskService.GetTask(taskid);
            if (task.Status != Core.Models.Enums.Status.Todo)
            {
                return BadRequest("Task can't be deleted because it has started.");
            }
            var task1 = await _taskService.ISAParent(taskid);
            if (task1)
            {
                return BadRequest("Task can't be deleted because this task has a childern.");
            }

            //SignalR Part

            var users = _assignTaskRepository.GetUsersIdByTaskId(taskid);
            string type = "Delete";
            var user = await _userManager.GetUserAsync(User);
            string NotificationMessage = $"{user.FullName} Deleted Task : {task.Title}";
            _notificationService.sendSignalRNotificationAsync(users, user.Id, type, NotificationMessage, taskid);


            //Delete Task
            await eventRepository.DeleteAssignTaskAsync(taskid);
            await _taskService.DeleteDepend(taskid);
            await _taskService.Delete(taskid);
            return Ok();
        }
        [DisplayName("Load Nodes For Add Dependencies")]
        public async Task<IActionResult> Loadnodes(int id)
        {
            var taskViewDeps = (await _taskService.Loadnodes(id)).Select(n =>
            {
                return new TaskDendenciesViewModel
                {
                    Id = n.TaskId,
                    Name = n.Name,
                    IsSelected = n.IsSelected,
                    DependencyType = n.DependencyType
                };
            }).ToList();
            return PartialView("_TaskDend", taskViewDeps);
        }
        [DisplayName("Add Dependencies")]
        [HttpPost]
        public async Task<IActionResult> SaveSelectedTasks(int SelectedTaskId, List<int> selectedTaskIds, List<DependencyType> dependencyTypes)
        {
            await _taskService.SaveSelectedTasks(SelectedTaskId, selectedTaskIds, dependencyTypes);
            return Ok();
        }
        [DisplayName("View All Tasks")]
        public async Task<IActionResult> Index()
        {
            List<TaskViewModel> taskViewModels = new List<TaskViewModel>();
            var tasks = await _taskRepository.GetAllAsync();
            foreach (var task in tasks)
            {
                TaskViewModel taskVM = new TaskViewModel();

                taskVM.Id = task.Id;
                taskVM.Title = task.Title;
                taskVM.Description = task.Description;
                taskVM.StartDate = task.StartDate;
                taskVM.EndDate = task.EndDate;
                taskVM.Status = task.Status;
                taskVM.Priority = task.Priority;
                taskVM.CreatedById = task.CreatedById;
                taskVM.CreatedAt = task.CreatedAt;
                taskVM.UpdatedAt = task.UpdatedAt;
                taskVM.ProjectId = task.ProjectId;
                foreach (var assignment in task.Assignments)
                {
                    taskVM.AssignedToId.Add(assignment.UserId);
                }
                taskVM.ParentTaskId = task.ParentTaskId;
                taskVM.UpdatedById = task.UpdatedById;
                taskVM.ProjectName = task.Project.Name;

                taskViewModels.Add(taskVM);
            }
            ViewBag.Users = await _userManager.Users.ToListAsync();

            return View(taskViewModels);
        }
        [DisplayName("transfer project status")]
        public async Task<IActionResult> IncreaseStatus(int id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            var sucsessortasks = await _taskDependencyRepo.GetBySuccessorIdAsync(id);
            if (task == null)
            {
                return NotFound(new { message = "Task Not found" });
            }
            foreach (var sucsessortask in sucsessortasks)
            {

                if (sucsessortask.DependencyType == DependencyType.FinishToStart && sucsessortask.Predecessor.Status != Core.Models.Enums.Status.Done)
                {
                    return BadRequest(new { message = "This task depends on another task. You must finish it first." });
                }
            }
            if (task.Status == Core.Models.Enums.Status.Done)
            {
                return BadRequest(new { message = "Task is already completed." });
            }
            ++task.Status;
            await _taskRepository.UpdateAsync(task);

            return PartialView("PartialViews/_Status", task);
        }
        [HttpGet]
        [DisplayName("Create Task")]
        public async Task<IActionResult> Create()
        {
            //ViewBag.Users = await _userManager.Users.ToListAsync();
            ViewBag.Projects = await _projectRepository.GetAllAsyncWithoutInclude();
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TaskViewModel taskVM)
        {


            var userId = User.Claims.FirstOrDefault(u => u.Type == ClaimTypes.NameIdentifier).Value;
            taskVM.CreatedById = userId;
            if (!ModelState.IsValid)
            {
                //ViewBag.Users = await _userManager.Users.ToListAsync();
                ViewBag.Projects = await _projectRepository.GetAllAsyncWithoutInclude();
                return View(taskVM);

            }
            TaskModel task = new TaskModel
            {
                Title = taskVM.Title,
                Description = taskVM.Description,
                StartDate = taskVM.StartDate,
                EndDate = taskVM.EndDate,
                Status = taskVM.Status,
                Priority = taskVM.Priority,
                CreatedById = userId,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                ProjectId = taskVM.ProjectId,
                ParentTaskId = taskVM.ParentTaskId,
                UpdatedById = userId,
            };
            await _taskRepository.AddAsync(task);
            await _assignTaskRepository.AssignTasksToUserByIds(taskVM.AssignedToId, task, User);
            //task.Assignments = await _assignTaskRepository.FindTasksAssignedToUserByIds(taskVM.AssignedToId);

            //SignalR Part

            var user = await _userManager.GetUserAsync(User);
            string NotificationMessage = $"{user.FullName} Assigned new Task : {taskVM.Title}";
            string notificationType = "NewTask";
            _notificationService.sendSignalRNotificationAsync(taskVM.AssignedToId, userId, notificationType, NotificationMessage,task.Id);
            
            
           
            return RedirectToAction(nameof(Index));
        }
        [DisplayName("Edit Task")]
        public async Task<IActionResult> Edit([FromRoute] int id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null) return NotFound();
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            TaskViewModel taskVM = new TaskViewModel();

            taskVM.Id = task.Id;
            taskVM.Title = task.Title;
            taskVM.Description = task.Description;
            taskVM.StartDate = task.StartDate;
            taskVM.EndDate = task.EndDate;
            taskVM.Status = task.Status;
            taskVM.Priority = task.Priority;
            taskVM.CreatedById = task.CreatedById;
            taskVM.CreatedAt = task.CreatedAt;
            taskVM.UpdatedAt = task.UpdatedAt;
            taskVM.ProjectId = task.ProjectId;
            foreach (var assignment in task.Assignments)
            {
                taskVM.AssignedToId.Add(assignment.UserId);
            }
            taskVM.ParentTaskId = task.ParentTaskId;
            taskVM.UpdatedById = task.UpdatedById;
            taskVM.ProjectName = task.Project.Name;


            var tasks = await GetTaskByProject(task.ProjectId);
            ViewBag.Tasks = JsonSerializer.Deserialize<List<TaskViewModel>>(JsonSerializer.Serialize(tasks.Value));
            //ViewBag.Users = await _userManager.Users.ToListAsync();
            ViewBag.Projects = await _projectRepository.GetAllAsyncWithoutInclude();
            return View(taskVM);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(TaskViewModel taskVM, [FromRoute] int id)
        {
            if (taskVM.Id == id)
            {
                if (ModelState.IsValid)
                {
                    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    var _task = await _taskRepository.GetByIdAsync(taskVM.Id);
                    await _assignTaskRepository.ModifyTasksToUserByIds(userId, _task, taskVM.AssignedToId);
                    _task.Title = taskVM.Title;
                    _task.Description = taskVM.Description;
                    _task.StartDate = taskVM.StartDate;
                    _task.EndDate = taskVM.EndDate;
                    _task.Status = taskVM.Status;
                    _task.Priority = taskVM.Priority;
                    _task.Status = taskVM.Status;
                    _task.Priority = taskVM.Priority;
                    _task.ParentTaskId = taskVM.ParentTaskId;
                    await _taskRepository.UpdateAsync(_task);

                    //SignalR Part
                    //Notification notification;

                    var user = await _userManager.GetUserAsync(User);
                    string NotificationMessage = $"{user.FullName} Updated Assigned Task : {taskVM.Title}";
                    string type = "UpdateTask";
                    _notificationService.sendSignalRNotificationAsync(taskVM.AssignedToId, userId, type, NotificationMessage,taskVM.Id);
                    
                    return RedirectToAction(nameof(Index));
                }
            }
            //ViewBag.Users = await _userManager.Users.ToListAsync();
            ViewBag.Projects = await _projectRepository.GetAllAsyncWithoutInclude();
            return View(taskVM);
        }

        public async Task<JsonResult> GetTaskByProject(int id)
        {
            var tasks = await _taskRepository.GetAllTasksPerProject(id);
            List<TaskViewModel> taskViewModels = new List<TaskViewModel>();
            foreach (var task in tasks)
            {
                TaskViewModel taskVM = new TaskViewModel
                {
                    Id = task.Id,
                    Title = task.Title,
                };
                taskViewModels.Add(taskVM);
            }
            return Json(taskViewModels);
        }
        public async Task<JsonResult> ValidateStartDate(DateTime StartDate, int ProjectId)
        {
            var project = await _projectRepository.GetByIdAsync(ProjectId);

            if (StartDate < project.StartDate || StartDate > project.EndDate)
            {
                return Json(false);
            }

            return Json(true);
        }

        public async Task<JsonResult> ValidateEndDate(DateTime EndDate, DateTime StartDate, int ProjectId)
        {
            var project = await _projectRepository.GetByIdAsync(ProjectId);

            if (EndDate < project.StartDate || EndDate > project.EndDate || EndDate < StartDate)
            {
                return Json(false);
            }

            return Json(true);
        }
        public async Task<IActionResult> Filter(TaskFilterViewModel filter)
        {
            var query = _context.Tasks
                .Include(t => t.Assignments).Include(t => t.Project)
                //.ThenInclude(a=>a.Branch).ThenInclude(a => a.Department)
                .AsQueryable();


            if (filter.Status != 0)
                query = query.Where(t => t.Status == filter.Status);

            if (filter.StartDate.HasValue)
                query = query.Where(t => t.StartDate == filter.StartDate);

            if (filter.EndDate.HasValue)
                query = query.Where(t => t.EndDate == filter.EndDate);

            if (!string.IsNullOrEmpty(filter.AssignedToUserId))
                query = query.Where(t => t.Assignments.Any(a => a.UserId == filter.AssignedToUserId));

            //if (filter.DepartmentId.HasValue)
            //    query = query.Where(t => t.Assignments.Any(a=>a.Department.Id == filter.DepartmentId);

            //if (filter.BranchId.HasValue)
            //    query = query.Where(t => t.Assignments.Any(a=>a.Branch.Id== filter.BranchId);

            var result = await query.ToListAsync();
            var taskViewModels = new List<TaskViewModel>();
            foreach (var task in result)
            {
                TaskViewModel taskVM = new TaskViewModel();

                taskVM.Id = task.Id;
                taskVM.Title = task.Title;
                taskVM.Description = task.Description;
                taskVM.StartDate = task.StartDate;
                taskVM.EndDate = task.EndDate;
                taskVM.Status = task.Status;
                taskVM.Priority = task.Priority;
                taskVM.CreatedById = task.CreatedById;
                taskVM.CreatedAt = task.CreatedAt;
                taskVM.UpdatedAt = task.UpdatedAt;
                taskVM.ProjectId = task.ProjectId;
                foreach (var assignment in task.Assignments)
                {
                    taskVM.AssignedToId.Add(assignment.UserId);
                }
                taskVM.ParentTaskId = task.ParentTaskId;
                taskVM.UpdatedById = task.UpdatedById;
                taskVM.ProjectName = task.Project.Name;

                taskViewModels.Add(taskVM);
            }

            return PartialView("PartialViews/_TaskTable", taskViewModels);
        }



        //[HttpGet]
        //public async Task<IActionResult> KanbanForProject(int projectId)
        //{
        //    string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var tasks = await _taskService.TasksForUserInProject(projectId, userId);

        //    return View("KanbanBoard", tasks);
        //}

        //[HttpGet]
        //public async Task<IActionResult> KanbanForUser()
        //{
        //    string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        //    var tasks = await _taskService.TasksForUser(userId);

        //    return View("KanbanBoard", tasks);
        //}


        [HttpPost]
        public async Task<IActionResult> UpdateColumnOrder([FromBody] List<ColumnOrderUpdate> columnOrder)
        {
            try
            {
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { title = "Unauthorized", detail = "User is not authenticated." });
                }

                var strategy = _context.Database.CreateExecutionStrategy();

                var result = await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var preferences = await _context.UserColumnPreferences
                            .Where(u => u.UserId == userId)
                            .ToListAsync();

                        foreach (var update in columnOrder)
                        {
                            var preference = preferences.FirstOrDefault(p => p.Id == update.ColumnId);
                            if (preference == null)
                            {
                                Console.WriteLine($"❌ Column {update.ColumnId} not found in database");
                                return false;
                            }

                            Console.WriteLine($"Updating column {update.ColumnId} to order {update.Order}");
                            preference.Order = update.Order;
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        Console.WriteLine($"❌ Error: {ex.Message}");
                        return false;
                    }
                });

                if (!result)
                {
                    return BadRequest(new { title = "Update Failed", detail = "Unable to update column order." });
                }

                return Ok(new { title = "Success", detail = "Column order updated successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in UpdateColumnOrder: {ex.Message}");
                return StatusCode(500, new { title = "Server Error", detail = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateColumnDisplayName([FromBody] ColumnDisplayNameUpdate model)
        {
            try
            {
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var result = await _userColumnPreferenceService.UpdateDisplayName(userId, model.Status, model.DisplayName);
                return result ? Ok() : BadRequest(new { title = "Preference not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { title = "Server error", details = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> KanbanForProject(int projectId)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var tasks = await _taskService.TasksForUserInProject(projectId, userId);

            var columns = await _userColumnPreferenceService.GetUserColumns(userId);

            if (!columns.Any())
            {
                await _userColumnPreferenceService.InitializeDefaultColumns(userId);
                columns = await _userColumnPreferenceService.GetUserColumns(userId);
            }

            bool isProjectOwner = await _projectRepository.IsUserProjectOwnerAsync(projectId, userId);
            ViewBag.IsProjectOwner = isProjectOwner;

            var viewModel = new KanbanViewModel
            {
                Tasks = tasks,
                Columns = columns.OrderBy(c => c.Order).ToList()
            };

            return View("KanbanBoard", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> KanbanForUser()
        {
            try
            {
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { title = "Unauthorized", detail = "User is not authenticated." });
                }

                var tasks = await _taskService.TasksForUser(userId);

                var columns = await _userColumnPreferenceService.GetUserColumns(userId);

                if (!columns.Any())
                {
                    await _userColumnPreferenceService.InitializeDefaultColumns(userId);
                    columns = await _userColumnPreferenceService.GetUserColumns(userId);
                }

                ViewBag.IsProjectOwner = false;

                var viewModel = new KanbanViewModel
                {
                    Tasks = tasks,
                    Columns = columns.OrderBy(c => c.Order).ToList()
                };

                return View("KanbanBoard", viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Exception in KanbanForUser: {ex.Message}");
                return StatusCode(500, new { title = "Server Error", detail = ex.Message });
            }
        }

        [DisplayName("Kanban for Project Owner")]
        public async Task<IActionResult> KanbanForProjectOwner(int? projectId = null)
        {
            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var ownedProjects = await _projectRepository.GetProjectsByOwnerIdAsync(userId);

            if (!ownedProjects.Any())
            {
                return RedirectToAction("NotFound", "Home");
            }

            ViewBag.Projects = new SelectList(ownedProjects, "Id", "Name", projectId);

            KanbanViewModel viewModel = null;

            if (projectId.HasValue)
            {
                bool isProjectOwner = await _projectRepository.IsUserProjectOwnerAsync(projectId.Value, userId);
                ViewBag.IsProjectOwner = isProjectOwner;

                if (!isProjectOwner)
                {
                    return Forbid();
                }

                List<Core.Models.Task> tasks = await _taskService.TasksForProject(projectId.Value);

                var columns = await _userColumnPreferenceService.GetUserColumns(userId);
                if (!columns.Any())
                {
                    await _userColumnPreferenceService.InitializeDefaultColumns(userId);
                    columns = await _userColumnPreferenceService.GetUserColumns(userId);
                }

                viewModel = new KanbanViewModel
                {
                    Tasks = tasks,
                    Columns = columns.OrderBy(c => c.Order).ToList(),
                    SelectedProjectId = projectId
                };
            }

            return View("KanbanForProjectOwner", viewModel);
        }


        //[HttpPost]
        //public async Task<IActionResult> UpdateStatus(int id, Core.Models.Enums.Status status)
        //{
        //    var task = await _taskRepository.GetByIdAsync(id);
        //    if (task == null) return NotFound();

        //    task.Status = status;
        //    await _taskRepository.UpdateAsync(task);
        //    return Ok();
        //}

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, Core.Models.Enums.Status status)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null) return NotFound();

            // لو كان الهدف InProgress
            if (status == Core.Models.Enums.Status.InProgress)
            {
                var dependencies = await _taskDependencyRepo.GetBySuccessorIdAsync(id);

                foreach (var dep in dependencies)
                {
                    var predecessor = dep.Predecessor;
                    if (dep.DependencyType == DependencyType.FinishToStart)
                    {
                        if (predecessor.Status != Core.Models.Enums.Status.Done)
                        {
                            return BadRequest(new { message = $"This task depends on '{predecessor.Title}' (FinishToStart). You must finish it first." });
                        }
                    }
                    else if (dep.DependencyType == DependencyType.StartToStart)
                    {
                        if (predecessor.Status != Core.Models.Enums.Status.InProgress && predecessor.Status != Core.Models.Enums.Status.Done)
                        {
                            return BadRequest(new { message = $"This task depends on '{predecessor.Title}' (StartToStart). It must be In Progress or Done." });
                        }
                    }
                }
            }

            task.Status = status;
            await _taskRepository.UpdateAsync(task);
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> GetTask(int id)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null) return NotFound();

            return Json(new
            {
                id = task.Id,
                title = task.Title,
                priority = task.Priority.ToString(),
                status = task.Status.ToString()
            });
        }


        [HttpPost]
        public async Task<IActionResult> UpdateTask(int id, Core.Models.Enums.Status status)
        {
            var task = await _taskRepository.GetByIdAsync(id);
            if (task == null) return NotFound();

            task.Status = status;
            await _taskRepository.UpdateAsync(task);
            return Ok();
        }
        public async Task<IActionResult> GetUsersInProject(int ProjectId)
        {
            var project = await _projectRepository.GetByIdAsync(ProjectId);
            if (project is not null)
            {
                var users = _projectRepository.GetMembers(ProjectId);
                List<UserVM> userList = new List<UserVM>();
                foreach (var user in users)
                {
                    userList.Add(new UserVM
                    {
                        Id = user.Id,
                        FullName = user.FullName
                    });
                }
                return Json(userList);
            }
            else
            {
                return Json(new { message = "Select Project First" });
            }
        }

    }
}