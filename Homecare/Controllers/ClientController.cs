using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Homecare.Controllers
{
    public class ClientController : Controller
    {
        private readonly IAppointmentRepository _apptRepo;
        private readonly IAvailableSlotRepository _slotRepo;
        private readonly IUserRepository _userRepo;
        private readonly ICareTaskRepository _taskRepo;

        public ClientController(IAppointmentRepository apptRepo, IAvailableSlotRepository slotRepo, IUserRepository userRepo, ICareTaskRepository taskRepo)
        { _apptRepo = apptRepo; _slotRepo = slotRepo; _userRepo = userRepo; _taskRepo = taskRepo; }

        // /Client/Dashboard?clientId=10  (auth gelince oturumdan alınır)
        public async Task<IActionResult> Dashboard(int? clientId)
        {
            // id yoksa ilk client'ı kullan (demo)
            int id = clientId ?? (await _userRepo.GetByRoleAsync(UserRole.Client)).First().UserId;
            var list = await _apptRepo.GetByClientAsync(id);
            var now = DateTime.Now;

            var upcoming = list.Where(a => a.AvailableSlot != null &&
                                           a.AvailableSlot.Day.ToDateTime(a.AvailableSlot.EndTime) >= now)
                               .OrderBy(a => a.AvailableSlot!.Day).ThenBy(a => a.AvailableSlot!.StartTime)
                               .ToList();
            var past = list.Where(a => a.AvailableSlot != null &&
                                       a.AvailableSlot.Day.ToDateTime(a.AvailableSlot.EndTime) < now)
                           .OrderByDescending(a => a.AvailableSlot!.Day).ThenByDescending(a => a.AvailableSlot!.StartTime)
                           .ToList();

            ViewBag.ClientId = id;
            ViewBag.Upcoming = upcoming;
            ViewBag.Past = past;
            return View();
        }

        // BOOK (day -> slots -> tasks)
        [HttpGet]
        public async Task<IActionResult> Create(int clientId, string? day = null)
        {
            ViewBag.ClientId = clientId;

            var freeDays = await _slotRepo.GetFreeDaysAsync();
            ViewBag.DayOptions = new SelectList(freeDays.Select(d => d.ToString("yyyy-MM-dd")));

            if (!string.IsNullOrEmpty(day) && DateOnly.TryParse(day, out var d))
            {
                var slots = await _slotRepo.GetFreeSlotsByDayAsync(d);
                ViewBag.SlotOptions = new SelectList(slots.Select(s => new
                {
                    s.AvailableSlotId,
                    Label = $"{s.Day:yyyy-MM-dd} {s.StartTime}-{s.EndTime} ({s.Personnel?.Name})"
                }), "AvailableSlotId", "Label");
            }
            else
            {
                ViewBag.SlotOptions = new SelectList(Enumerable.Empty<SelectListItem>());
            }

            ViewBag.TaskOptions = new MultiSelectList(await _taskRepo.GetAllAsync(), "CareTaskId", "Description");
            return View(new Appointment { ClientId = clientId, Status = AppointmentStatus.Scheduled });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment model, int[] selectedTaskIds)
        {
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId))
            {
                ModelState.AddModelError(nameof(model.AvailableSlotId), "Selected slot is no longer available.");
            }
            if (!ModelState.IsValid)
            {
                // yeniden doldur
                ViewBag.ClientId = model.ClientId;
                var freeDays = await _slotRepo.GetFreeDaysAsync();
                ViewBag.DayOptions = new SelectList(freeDays.Select(d => d.ToString("yyyy-MM-dd")));
                ViewBag.SlotOptions = new SelectList(Enumerable.Empty<SelectListItem>());
                ViewBag.TaskOptions = new MultiSelectList(await _taskRepo.GetAllAsync(), "CareTaskId", "Description", selectedTaskIds);
                return View(model);
            }

            await _apptRepo.AddAsync(model);
            // TaskList eklemek için doğrudan context yerine repo olsa da basit tutuyoruz:
            // (İleride TaskList için repo ekleyebiliriz)
            // Şimdilik, task ekleme AppointmentController üzerinden değil DBInit'e benzer şekilde yapılabilir;
            // ama minimumda randevu yaratmak yeterli.
            TempData["Message"] = "Appointment booked.";
            return RedirectToAction(nameof(Dashboard), new { clientId = model.ClientId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _apptRepo.GetAsync(id);
            if (a == null) return NotFound();
            int clientId = a.ClientId;
            await _apptRepo.DeleteAsync(a);
            TempData["Message"] = "Appointment deleted.";
            return RedirectToAction(nameof(Dashboard), new { clientId });
        }
    }
}
