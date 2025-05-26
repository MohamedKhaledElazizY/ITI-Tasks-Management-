using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartTask.Core.IRepositories;

namespace SmartTask.Web.Controllers
{
    [Authorize]
    public class EmployeeController : Controller
    {

        private readonly IUserRepository _userRepository;
        public EmployeeController(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }
        public async Task<IActionResult> Index(string id)
        {

            var User = await _userRepository.GetWithDetailsAsync(id);
            if (User == null)
            {
                return NotFound();
            }
            return View(User);
        }
    }
    
}
