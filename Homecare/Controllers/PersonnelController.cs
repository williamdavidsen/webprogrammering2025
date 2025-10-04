using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Mvc;

namespace Homecare.Controllers
{
    public class PersonnelController : Controller
    {
        private readonly IAppointmentRepository _apptRepo;
        private readonly IAvailableSlotRepository _slotRepo;
        private readonly IUserRepository _userRepo;

        public PersonnelController(IAppointmentRepository apptRepo, IAvailableSlotRepository slotRepo, IUserRepository userRepo)
        { _apptRepo = apptRepo; _slotRepo = slotRepo; _userRepo = userRepo; }

        // /Personnel/Dashboard?personnelId=2
        public async Task<IActionResult> Dashboard(int? personnelId)
        {
            int id = personnelId ?? (await _userRepo.GetByRoleAsync(UserRole.Personnel)).First().UserId;
            var list = await _apptRepo.GetByPersonnelAsync(id);
            var now = DateTime.Now;

            var upcoming = list.Where(a => a.AvailableSlot!.Day.ToDateTime(a.AvailableSlot!.EndTime) >= now)
                               .OrderBy(a => a.AvailableSlot!.Day).ThenBy(a => a.AvailableSlot!.StartTime).ToList();
            var past = list.Where(a => a.AvailableSlot!.Day.ToDateTime(a.AvailableSlot!.EndTime) < now)
                           .OrderByDescending(a => a.AvailableSlot!.Day).ThenByDescending(a => a.AvailableSlot!.StartTime).ToList();

            ViewBag.PersonnelId = id;
            ViewBag.Upcoming = upcoming;
            ViewBag.Past = past;
            return View();
        }

        // Hemşire sadece GÜN seçer → 3 slot otomatik
        [HttpGet]
        public IActionResult CreateDay(int personnelId)
        {
            ViewBag.PersonnelId = personnelId;
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDay(int personnelId, DateOnly day)
        {
            // aynı güne önceden slot var mı?
            var existing = await _slotRepo.GetFreeSlotsByDayAsync(day);
            if (existing.Any(s => s.PersonnelId == personnelId))
            {
                TempData["Error"] = "This day already exists for you.";
                return RedirectToAction(nameof(Dashboard), new { personnelId });
            }

            var presets = new (TimeOnly Start, TimeOnly End)[]
            {
                (new TimeOnly(9,  0), new TimeOnly(11, 0)),
                (new TimeOnly(12, 0), new TimeOnly(14, 0)),
                (new TimeOnly(16, 0), new TimeOnly(18, 0))
            };

            var slots = presets.Select(p => new AvailableSlot
            {
                PersonnelId = personnelId,
                Day = day,
                StartTime = p.Start,
                EndTime = p.End
            });

            await _slotRepo.AddRangeAsync(slots);
            TempData["Message"] = $"{day:yyyy-MM-dd} created with 3 slots.";
            return RedirectToAction(nameof(Dashboard), new { personnelId });
        }
    }
}
