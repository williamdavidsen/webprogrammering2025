using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Homecare.Controllers
{
    public class AdminController : Controller
    {
        private readonly IUserRepository _userRepo;
        private readonly IAppointmentRepository _apptRepo;
        private readonly IAvailableSlotRepository _slotRepo;

        public AdminController(
            IUserRepository userRepo,
            IAppointmentRepository apptRepo,
            IAvailableSlotRepository slotRepo)
        {
            _userRepo = userRepo;
            _apptRepo = apptRepo;
            _slotRepo = slotRepo;
        }

        // /Admin/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            ViewBag.OwnerName = User?.Identity?.Name ?? "Administrator";
            ViewBag.OwnerRole = "Admin";
            // 1) Sayılar (özet)
            var clients = await _userRepo.GetByRoleAsync(UserRole.Client);
            var personnels = await _userRepo.GetByRoleAsync(UserRole.Personnel);
            var appts = await _apptRepo.GetAllAsync();

            ViewBag.TotalClients = clients.Count;
            ViewBag.TotalPersonnels = personnels.Count;
            ViewBag.TotalAppts = appts.Count;

            // 2) Hızlı geçiş dropdown’ları
            ViewBag.ClientsDD = new SelectList(clients, "UserId", "Name");
            ViewBag.PersonnelsDD = new SelectList(personnels, "UserId", "Name");

            // 3) Yaklaşan randevular (en fazla 10)
            var now = DateTime.Now;
            var upcoming = appts
                .Where(a => a.AvailableSlot != null &&
                            a.AvailableSlot.Day.ToDateTime(a.AvailableSlot.EndTime) >= now)
                .OrderBy(a => a.AvailableSlot!.Day)
                .ThenBy(a => a.AvailableSlot!.StartTime)
                .Take(10)
                .ToList();
            ViewBag.Upcoming = upcoming;

            // 4) Boş slotlar (önümüzdeki 14 gün)
            const int rangeDays = 14;
            var start = DateOnly.FromDateTime(DateTime.Today);
            var freeSlots = new List<AvailableSlot>();
            for (int i = 0; i < rangeDays; i++)
            {
                var day = start.AddDays(i);
                var list = await _slotRepo.GetFreeSlotsByDayAsync(day);
                if (list != null && list.Any()) freeSlots.AddRange(list);
            }
            ViewBag.FreeSlots = freeSlots
                .OrderBy(s => s.Day)
                .ThenBy(s => s.StartTime)
                .ToList();

            return View();
        }
    }
}
