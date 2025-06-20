﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using SmartTask.BL.IServices;
using SmartTask.Core.IRepositories;
using SmartTask.Core.Models;
using SmartTask.Core.Models.Enums;
using SmartTask.Core.Models.ServiceDto;


namespace SmartTask.BL.Services
{
    public class TaskService : ITaskService
    {
        private ITaskRepository _taskRepository;
        private ICommentRepository _commentRepository;
        private IAttachmentRepository _attachmentRepository;
        private ITaskDependencyRepository _taskDependencyRepository;
        private readonly UserManager<ApplicationUser> _userManager;

        public TaskService(ITaskRepository taskRepository, ICommentRepository commentRepository, IAttachmentRepository attachmentRepository, ITaskDependencyRepository taskDependencyRepository, UserManager<ApplicationUser> userManager)
        {
            _taskRepository = taskRepository;
            _commentRepository = commentRepository;
            _attachmentRepository = attachmentRepository;
            _taskDependencyRepository = taskDependencyRepository;
            _userManager = userManager;
        }

        public async Task<Core.Models.Task> Details(int id)
        {
            var task = await _taskRepository.GetWithDetailsAsync(id);
            return task;
        }
        public async Task<bool> ISAParent(int id)
        {
            var task = await _taskRepository.ISAParent(id);
            return task;
        }
        public async Task<Comment> AddComment(int taskId, string authorId, string content)
        {
            var comment = new Comment
            {
                TaskId = taskId,
                AuthorId = authorId,
                Content = content.Trim(),
                CreatedAt = DateTime.Now
            };
            await _commentRepository.AddAsync(comment);

            return await _commentRepository.GetByIdAsync(comment.Id);
        }

        public async Task<Attachment> AddAttachment(int taskId, IFormFile file, string userId, string webRootPath)
        {
            var uploadsFolder = Path.Combine(webRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var attachment = new Attachment
            {
                TaskId = taskId,
                FileName = file.FileName,
                FilePath = $"/uploads/{uniqueFileName}",
                UploadedById = userId,
                CreatedAt = DateTime.Now
            };
            await _attachmentRepository.AddAsync(attachment);
            return await _attachmentRepository.GetByIdAsync(attachment.Id);
        }

        public async Task<List<Core.Models.Task>> TasksForUserInProject(int projectId, string userId)
        {
            var tasks = (await _taskRepository.GetByProjectIdAsync(projectId))
                .Where(t => t.Assignments.Any(a => a.UserId == userId))
                .ToList();

            foreach (var task in tasks)
            {
                if (task.Assignments == null)
                {
                    task.Assignments = new List<AssignTask>();
                }
                else
                {
                    foreach (var assignment in task.Assignments)
                    {
                        if (assignment.User == null)
                        {
                            assignment.User = await _userManager.FindByIdAsync(assignment.UserId);
                        }
                    }
                }
            }

            return tasks;
        }

        public async Task<List<Core.Models.Task>> TasksForProject(int projectId)
        {
            var tasks = (await _taskRepository.GetByProjectIdAsync(projectId)).ToList();
            return tasks;
        }

        public async Task<List<Core.Models.Task>> TasksForUser(string userId)
        {
            var tasks = (await _taskRepository.GetByAssignedToIdAsync(userId)).ToList();
            return tasks;
        }

        public async Task<int> NumofDepend(int taskId)
        {
            return (await _taskDependencyRepository.GetByPredecessorIdAsync(taskId)).Count();
        }

        public async Task<Core.Models.Task> GetTask(int taskId)
        {
            return (await _taskRepository.GetByIdAsync(taskId));
        }

        public async System.Threading.Tasks.Task DeleteDepend(int taskId)
        {
            var depend = (await _taskDependencyRepository.GetByPredecessorIdAsync(taskId)).ToList();
            await _taskDependencyRepository.DeleteRangeAsync(depend);

            var depend2 = (await _taskDependencyRepository.GetBySuccessorIdAsync(taskId)).ToList();
            await _taskDependencyRepository.DeleteRangeAsync(depend2);
        }

        public async System.Threading.Tasks.Task Delete(int taskId)
        {
            await _taskRepository.DeleteAsync(taskId);
        }

        public async Task<List<TaskDenpendDto>> Loadnodes(int id)
        {
            var graph = new Dictionary<int, List<int>>();
            var visited = new HashSet<int>();
            var task = await _taskRepository.GetByIdAsync(id);
            var allTasks = await _taskRepository.GetByProjectIdAsync(task.ProjectId);

            var taskdepn = (await _taskDependencyRepository.GetAllAsync()).ToList()
                .Where(x => x.Predecessor.ProjectId == task.ProjectId).ToList();//brings all pre that are in the same proj but doesnt have to be pre of task

            taskdepn.ForEach(t =>
                {
                    if (!graph.ContainsKey(t.PredecessorId))
                    {
                        graph[t.PredecessorId] = new List<int>();
                    }
                    graph[t.PredecessorId].Add(t.SuccessorId);
                });

            visited.DFS(id, graph);

            var notReachable = allTasks.ToList().ExceptBy(visited, e => e.Id);
            var existingDeps = (await _taskDependencyRepository.GetBySuccessorIdAsync(id)).Select(e => e.PredecessorId).ToList();
            var DependancyType = await _taskDependencyRepository.GetBySuccessorIdAsync(id);

            var taskViewDeps = notReachable.Select(n => new TaskDenpendDto
            {
                TaskId = n.Id,
                Name = n.Title,
                IsSelected = existingDeps.Contains(n.Id),
                DependencyType = DependancyType.Where(td=>td.PredecessorId == n.Id).Select(td=>td.DependencyType).FirstOrDefault()
            }).ToList();

            return taskViewDeps;
        }
         
        public async System.Threading.Tasks.Task SaveSelectedTasks(int SelectedTaskId, List<int> selectedTaskIds,List<DependencyType> dependencyTypes)
        {
            var existingDependencies = await _taskDependencyRepository.GetBySuccessorIdAsync(SelectedTaskId);
            var ZipList = selectedTaskIds.Zip(dependencyTypes, (id, type) => new { id, type }).ToList();
            foreach (var selectedId in selectedTaskIds)
            {
                var DependancyType = ZipList.Where(t => t.id == selectedId).Select(t => t.type).FirstOrDefault();
                var TaskDependency = existingDependencies.FirstOrDefault(td => td.PredecessorId == selectedId);
                if (!existingDependencies.Any(td => td.PredecessorId == selectedId))
                {
                    await _taskDependencyRepository.AddAsync(new TaskDependency
                    {
                        SuccessorId = SelectedTaskId,
                        PredecessorId = selectedId,
                        DependencyType = DependancyType // Assuming a default dependency type
                    });
                }
                else if (TaskDependency?.DependencyType != DependancyType)
                {
                    TaskDependency.DependencyType = DependancyType;
                    await _taskDependencyRepository.UpdateAsync(TaskDependency);
                }

            }

            foreach (var dependency in existingDependencies)
            {
                if (!selectedTaskIds.Contains(dependency.PredecessorId))
                {
                    await _taskDependencyRepository.DeleteAsync(dependency.Id);
                }
            }
        }
    }
}