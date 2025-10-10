using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Homecare.Controllers
{
    [Route("Client")]
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

        // ---- Helper: clientId yoksa ilk müşteriyi bul ----
        private async Task<int> ResolveClientId(int? clientId)
        {
            if (clientId.HasValue) return clientId.Value;
            var first = (await _userRepo.GetByRoleAsync(UserRole.Client)).FirstOrDefault();
            if (first == null) throw new InvalidOperationException("No client exists in the system.");
            return first.UserId;
        }

        private async Task SetOwnerForClientAsync(int clientId)
        {
            var u = await _userRepo.GetAsync(clientId);
            ViewBag.OwnerName = u?.Name ?? $"Client #{clientId}";
            ViewBag.OwnerRole = "Client";
        }

        // ----------------- DASHBOARD -----------------
        // /Client/Dashboard  veya  /Client/Dashboard/10
        [HttpGet("Dashboard/{clientId:int?}")]
        public async Task<IActionResult> Dashboard(int? clientId)
        {
            int id = await ResolveClientId(clientId);
            await SetOwnerForClientAsync(id);

            var list = await _apptRepo.GetByClientAsync(id);
            var now = DateTime.Now;

            var upcoming = list.Where(a => a.AvailableSlot != null &&
                                           a.AvailableSlot.Day.ToDateTime(a.AvailableSlot.EndTime) >= now)
                               .OrderBy(a => a.AvailableSlot!.Day)
                               .ThenBy(a => a.AvailableSlot!.StartTime)
                               .ToList();

            var past = list.Where(a => a.AvailableSlot != null &&
                                       a.AvailableSlot.Day.ToDateTime(a.AvailableSlot.EndTime) < now)
                           .OrderByDescending(a => a.AvailableSlot!.Day)
                           .ThenByDescending(a => a.AvailableSlot!.StartTime)
                           .ToList();

            ViewBag.ClientId = id;
            ViewBag.Upcoming = upcoming;
            ViewBag.Past = past;
            return View();
        }

        // ----------------- CREATE (GET) -----------------
        // /Client/Create/10   (day querystring ile gelebilir)
        [HttpGet("Create/{clientId:int}")]
        public async Task<IActionResult> Create(int clientId, string? day = null)
        {
            await SetOwnerForClientAsync(clientId);
            ViewBag.ClientId = clientId;

            // 1) Boş slotu olan günleri takvime ver
            var freeDays = await _slotRepo.GetFreeDaysAsync(); // List<DateOnly>
            ViewBag.FreeDays = freeDays
                .Select(d => d.ToString("yyyy-MM-dd"))
                .ToList();

            ViewBag.InitialMonth = (freeDays.Any()
                    ? freeDays.Min()
                    : DateOnly.FromDateTime(DateTime.Today))
                .ToString("yyyy-MM-01");

            // 2) Eski dropdown fallback (opsiyonel)
            var freeSet = freeDays.ToHashSet();
            const int rangeDays = 14;
            var start = DateOnly.FromDateTime(DateTime.Today);
            var dayItems = Enumerable.Range(0, rangeDays)
                .Select(i => start.AddDays(i))
                .Select(d => new SelectListItem
                {
                    Text = d.ToString("yyyy-MM-dd dddd"),
                    Value = d.ToString("yyyy-MM-dd"),
                    Disabled = !freeSet.Contains(d)
                })
                .ToList();
            ViewBag.DayItems = dayItems;

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

            // 3) Görevler
            ViewBag.TaskOptions = new MultiSelectList(
                await _taskRepo.GetAllAsync(), "CareTaskId", "Description"
            );

            return View(new Appointment { ClientId = clientId, Status = AppointmentStatus.Scheduled });
        }

        // ----------------- CREATE (POST) -----------------
        // /Client/Create/{clientId}  → POST
        [HttpPost("Create/{clientId:int}"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            int clientId,
            [FromForm] Appointment model,
            [FromForm] int[]? selectedTaskIds)
        {
            // Route'tan gelen id’yi modele yaz
            model.ClientId = clientId;

            // Slot halen uygun mu?
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId))
                ModelState.AddModelError(nameof(model.AvailableSlotId), "Selected slot is no longer available.");

            if (!ModelState.IsValid)
                return await RefillCreateForm(model, selectedTaskIds ?? Array.Empty<int>());

            await _apptRepo.AddAsync(model);

            // (İleride: selectedTaskIds için TaskList ekleme burada yapılır)
            if (TempData != null)
                TempData["Message"] = "Appointment booked.";
            return RedirectToAction(nameof(Dashboard), new { clientId });
        }


        private async Task<IActionResult> RefillCreateForm(Appointment model, int[] selectedTaskIds)
        {
            await SetOwnerForClientAsync(model.ClientId);
            ViewBag.ClientId = model.ClientId;

            var freeDays = await _slotRepo.GetFreeDaysAsync() ?? new List<DateOnly>();
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
         })
         .ToList();

            ViewBag.SlotItems = new List<SelectListItem>();
            var selected = selectedTaskIds ?? Enumerable.Empty<int>();
            ViewBag.TaskOptions = new MultiSelectList(
        await _taskRepo.GetAllAsync() ?? new List<CareTask>(),
        "CareTaskId", "Description",
        selected
    );

            return View("Create", model);
        }

        // ----------------- AJAX: seçilen günün boş slotlarını getir -----------------
        // /Client/SlotsForDay?day=2025-10-21
        [HttpGet("SlotsForDay")]
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
