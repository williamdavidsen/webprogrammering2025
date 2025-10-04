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

        public ClientController(
            IAppointmentRepository apptRepo,
            IAvailableSlotRepository slotRepo,
            IUserRepository userRepo,
            ICareTaskRepository taskRepo)
        {
            _apptRepo = apptRepo;
            _slotRepo = slotRepo;
            _userRepo = userRepo;
            _taskRepo = taskRepo;
        }

        // ----------------- DASHBOARD (değişmeden kalabilir) -----------------
        public async Task<IActionResult> Dashboard(int? clientId)
        {
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

        // ----------------- CREATE (GET) -----------------
        [HttpGet]
        public async Task<IActionResult> Create(int clientId, string? day = null)
        {
            ViewBag.ClientId = clientId;

            // 1) Repository'den "free" günleri çek
            var freeDays = await _slotRepo.GetFreeDaysAsync(); // HashSet’e alalım
            var freeSet = freeDays.ToHashSet();

            // 2) Bugünden itibaren N gün gösterelim (pasif/aktif)
            const int rangeDays = 14;
            var start = DateOnly.FromDateTime(DateTime.Today);
            var dayItems = Enumerable.Range(0, rangeDays)
                .Select(i => start.AddDays(i))
                .Select(d => new SelectListItem
                {
                    Text = d.ToString("yyyy-MM-dd dddd"),
                    Value = d.ToString("yyyy-MM-dd"),
                    Disabled = !freeSet.Contains(d) // boş slot yoksa pasif
                })
                .ToList();

            ViewBag.DayItems = dayItems;

            // 3) Seçili güne göre slotları (boş olanları) dolduralım
            if (!string.IsNullOrEmpty(day) && DateOnly.TryParse(day, out var sel))
            {
                var slots = await _slotRepo.GetFreeSlotsByDayAsync(sel);
                ViewBag.SlotItems = slots.Select(s => new SelectListItem
                {
                    Value = s.AvailableSlotId.ToString(),
                    Text = $"{s.StartTime:HH\\:mm}-{s.EndTime:HH\\:mm} ({s.Personnel?.Name})"
                }).ToList();
            }
            else
            {
                ViewBag.SlotItems = new List<SelectListItem>();
            }

            // 4) Görevler
            ViewBag.TaskOptions = new MultiSelectList(
                await _taskRepo.GetAllAsync(), "CareTaskId", "Description");

            // Form model
            return View(new Appointment
            {
                ClientId = clientId,
                Status = AppointmentStatus.Scheduled
            });
        }

        // ----------------- CREATE (POST) -----------------
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment model, int[] selectedTaskIds)
        {
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId))
            {
                ModelState.AddModelError(nameof(model.AvailableSlotId), "Selected slot is no longer available.");
            }
            if (!ModelState.IsValid)
            {
                // tekrar doldur
                return await RefillCreateForm(model, selectedTaskIds);
            }

            await _apptRepo.AddAsync(model);
            // (TaskList ekleme adımı ileride; MVP için randevu oluşturmak yeterli)
            TempData["Message"] = "Appointment booked.";
            return RedirectToAction(nameof(Dashboard), new { clientId = model.ClientId });
        }

        private async Task<IActionResult> RefillCreateForm(Appointment model, int[] selectedTaskIds)
        {
            ViewBag.ClientId = model.ClientId;

            var freeDays = await _slotRepo.GetFreeDaysAsync();
            var freeSet = freeDays.ToHashSet();

            const int rangeDays = 14;
            var start = DateOnly.FromDateTime(DateTime.Today);
            ViewBag.DayItems = Enumerable.Range(0, rangeDays)
                .Select(i => start.AddDays(i))
                .Select(d => new SelectListItem
                {
                    Text = d.ToString("yyyy-MM-dd dddd"),
                    Value = d.ToString("yyyy-MM-dd"),
                    Disabled = !freeSet.Contains(d)
                }).ToList();

            ViewBag.SlotItems = new List<SelectListItem>();
            ViewBag.TaskOptions = new MultiSelectList(
                await _taskRepo.GetAllAsync(), "CareTaskId", "Description", selectedTaskIds);

            return View("Create", model);
        }

        // ----------------- AJAX: seçilen günün boş slotlarını getir -----------------
        [HttpGet]
        public async Task<IActionResult> SlotsForDay(string day)
        {
            if (!DateOnly.TryParse(day, out var d))
                return Json(Array.Empty<object>());

            var slots = await _slotRepo.GetFreeSlotsByDayAsync(d);
            var data = slots.Select(s => new
            {
                id = s.AvailableSlotId,
                label = $"{s.StartTime:HH\\:mm}-{s.EndTime:HH\\:mm} ({s.Personnel?.Name})"
            });

            return Json(data);
        }
    }
}
