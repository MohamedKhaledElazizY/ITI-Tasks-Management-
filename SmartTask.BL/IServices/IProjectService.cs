﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Task = System.Threading.Tasks.Task;
using SmartTask.Core.Models;
using SmartTask.Bl.Helpers;



namespace SmartTask.BL.IServices
{
    public interface IProjectService
    {
        Task<PaginatedList<Project>> GetFilteredProjectsAsync(string searchString, int page, int pageSize);
        Task<PaginatedList<Project>> GetFilteredProjectsAsync(string searchString, int? departmentId, int page, int pageSize);
        Task<PaginatedList<Project>> GetFilteredByDepartmentProjectsAsync(string searchString, int? departmentId, int? branchId, int page, int pageSize);

        Task<Project> AddProjectAsync(Project project);
        Task<Project> GetProjectByIdAsync(int id);
        Task<IEnumerable<Project>> GetAllProjectsAsync();
        Task UpdateProjectAsync(Project project);
        Task DeleteProjectAsync(int id);

        Task<bool> AddMemberAsync(int projectId, string userId);

        // To Display list of projects for a specific user
        Task<List<Project>> GetUserProjectsAsync(string userId);

        //To Diplay Details of a specific project for a specific user
        Task<Project> GetProjectDetailsAsync(int projectId, string userId);
    }
}
