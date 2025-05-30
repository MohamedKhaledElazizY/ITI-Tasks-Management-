using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Graph.Models;
using SmartTask.Bl.Helpers;
using SmartTask.Bl.IServices;
using SmartTask.Bl.Services;
using SmartTask.BL.IServices;
using SmartTask.Core.IRepositories;
using SmartTask.Core.Models;
using SmartTask.DataAccess.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace SmartTask.BL.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IDepartmentRepository _departmentRepository;
        private readonly IBranchRepository _branchRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IPaginatedService<ApplicationUser> _paginatedService;

        public UserService(
            UserManager<ApplicationUser> userManager,
            IPaginatedService<ApplicationUser> paginatedService,
            IDepartmentRepository departmentRepository,
            IBranchRepository branchRepository,
            IProjectRepository projectRepository)
        {
            _userManager = userManager;
            _paginatedService = paginatedService;
            _departmentRepository = departmentRepository;
            _branchRepository = branchRepository;
            _projectRepository = projectRepository;
        }

        public async Task<IEnumerable<ApplicationUser>> GetAllAsync()
        {
            return await _userManager.Users
                .Include(u => u.Department)
                .ToListAsync();
        }
        public async Task< PaginatedList<ApplicationUser>> GetAll(int page, int pageSize)
        {
            return await _paginatedService.GetFilteredAsync(null, page, pageSize);
        }
        public async Task<ApplicationUser> GetByIdAsync(string id)
        {
            return await _userManager.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<bool> UpdateAsync(ApplicationUser updatedUser)
        {
            var user = await _userManager.FindByIdAsync(updatedUser.Id);
            if (user == null) return false;

            user.FullName = updatedUser.FullName;
            user.DepartmentId = updatedUser.DepartmentId;
            user.BranchId = updatedUser.BranchId;
            user.updatedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return false;

            var branches = await _branchRepository.GetAllAsync();
            var relatedBranches =  branches.Where(b => b.ManagerId  == user.Id).ToList();
            foreach (var branch in relatedBranches)
            {
                branch.ManagerId = null;
            }

            var departments = await _departmentRepository.GetAllAsync();
            var relatedDepartments = departments.Where(d => d.ManagerId == user.Id).ToList();
            foreach (var department in relatedDepartments)
            {
                department.ManagerId = null;
            }

            var isOwner = await _projectRepository.IsUserOwnerAsync(id);
            if (isOwner)
                throw new InvalidOperationException("لا يمكن حذف المستخدم لأنه مالك لمشروع. يرجى نقل الملكية أولاً.");

            await _branchRepository.UpdateRangeAsync(relatedBranches);
            await _departmentRepository.UpdateRangeAsync(relatedDepartments);
            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded;
        }

        public async Task<PaginatedList<ApplicationUser>> GetFilteredAsync(string searchString, int page, int pageSize)
        {
            var query = _userManager.Users.Include(u => u.Department).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                query = query.Where(u => u.FullName.Contains(searchString) || u.Email.Contains(searchString));
            }

            return await PaginatedList<ApplicationUser>.CreateAsync(query, page, pageSize);
        }

        public async Task<bool> AssignUserToDepartmentAsync(string userId, int departmentId)
        {
            var user = _userManager.Users.Include(u => u.Department).FirstOrDefault(u => u.Id == userId);
            if (user == null) return false;

            if (user.DepartmentId != null) return false;

            var department = await _departmentRepository.GetByIdAsync(departmentId);
            if (department == null) return false;

            user.DepartmentId = department.Id;

            var result = await _userManager.UpdateAsync(user);
            return result.Succeeded;
        }
        public async Task<PaginatedList<ApplicationUser>> GetUsersWithoutDepartemnt(int page, int pageSize)
        {
            var query = _userManager.Users
        .Where(u => u.DepartmentId == null)
        .Include(u => u.Branch)
            .ThenInclude(b => b.BranchDepartments)
                .ThenInclude(bd => bd.Department)
        .AsQueryable();
            return await PaginatedList<ApplicationUser>.CreateAsync(query, page, pageSize);
        }
    }
}
