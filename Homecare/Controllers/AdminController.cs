using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Mvc;

namespace Homecare.Controllers
{
    public class AdminController : Controller
    {
        private readonly IUserRepository _userRepo;
        private readonly IAvailableSlotRepository _slotRepo;
        private readonly IAppointmentRepository _apptRepo;

        public AdminController(IUserRepository userRepo, IAvailableSlotRepository slotRepo, IAppointmentRepository apptRepo)
        { _userRepo = userRepo; _slotRepo = slotRepo; _apptRepo = apptRepo; }

        public async Task<IActionResult> Dashboard()
        {
            var users = await _userRepo.GetByRoleAsync(UserRole.Client);
            ViewBag.ClientCount = users.Count;
            ViewBag.PersonnelCount = (await _userRepo.GetByRoleAsync(UserRole.Personnel)).Count;
            ViewBag.AdminCount = (await _userRepo.GetByRoleAsync(UserRole.Admin)).Count;

            ViewBag.SlotCount = (await _slotRepo.GetAllAsync()).Count;
            ViewBag.AppointmentCount = (await _apptRepo.GetAllAsync()).Count;

            return View();
        }
    }
}
