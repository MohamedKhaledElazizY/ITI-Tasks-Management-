using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph.Security.Labels.Categories.Item.Subcategories.Item;
using SmartTask.Core.IRepositories;
using SmartTask.Web.ViewModels;
using System.Threading.Tasks;

namespace SmartTask.Web.Controllers
{
    public class CalendarController : Controller
    {
        private readonly ITaskRepository _taskRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ITaskDependencyRepository _taskDependencyRepository;

        public CalendarController(ITaskRepository taskRepository,IProjectRepository projectRepository,ITaskDependencyRepository  taskDependencyRepository)
        {
            _taskRepository = taskRepository;
            _projectRepository = projectRepository;
            _taskDependencyRepository = taskDependencyRepository;
        }
        public async Task<IActionResult> Index()
        {


            var projects = await _projectRepository.GetAllAsyncWithoutInclude();

            if (projects == null || !projects.Any())
                return NotFound("No projects found.");

            ViewBag.Projects = projects;    
            return View();
        }
        public async Task<IActionResult> CalendarByUserId(string id)
        {


            var projects = await _projectRepository. GetUserProjectsAsync(id);

            if (projects == null || !projects.Any())
                return NotFound("No projects found.");

            ViewBag.Projects = projects;
            return View();
        }
        public async Task<IActionResult> GetProjectById(int ProjectId)
        
        {
            var project = await _projectRepository.GetByIdAsync(ProjectId);
            var Duration = new { StartDate=project.StartDate?.ToString("yyyy-MM-dd"), EndDate= project.EndDate?.ToString("yyyy-MM-dd") };
            return Json(Duration);
        }
        public async Task<IActionResult> GetTasksForProject(int ProjectId)
        {
            var tasks = await _taskRepository.GetAllTasksPerProject(ProjectId);
            var CalendarViewModels = new List<CalendarViewModel>();
            foreach (var task in tasks)
            {
                var taskVM = new CalendarViewModel()
                {
                    Id = task.Id,
                    Title = task.Title,
                    Start = task.StartDate?.ToString("yyyy-MM-dd"),
                    End = task.EndDate?.ToString("yyyy-MM-dd"),
                   TaskStatus = task.Status
                };
                CalendarViewModels.Add(taskVM);
            }
            return Json(CalendarViewModels);
        }
        public async Task<IActionResult> UpdateTask(TaskCalendarUpdateVM TaskVM)
        {
            var taskDependancies = await _taskDependencyRepository.GetBySuccessorIdAsync(TaskVM.Id);
            if (taskDependancies == null)
                return NotFound("Couldnt Find Task");
            foreach (var Pretask in taskDependancies)
            {
                if (DateOnly.Parse(Pretask.Predecessor.StartDate.ToString().Split(' ')[0]) > TaskVM.Start)
                {
                    return BadRequest(new { message = $"the start Date of this task cannot precede its predecessor task{Pretask.Predecessor.Title}" });
                }

            }
            
            await _taskRepository.UpdateTaskDates(TaskVM.Id, TaskVM.Start, TaskVM.End);

            return Ok("Task Updated Successfully!");
        }
    }
}
