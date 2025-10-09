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

        public PersonnelController(
            IAppointmentRepository apptRepo,
            IAvailableSlotRepository slotRepo,
            IUserRepository userRepo)
        {
            _apptRepo = apptRepo;
            _slotRepo = slotRepo;
            _userRepo = userRepo;
        }

        // /Personnel/Dashboard?personnelId=2
        public async Task<IActionResult> Dashboard(int? personnelId)
        {
            int id = personnelId ?? (await _userRepo.GetByRoleAsync(UserRole.Personnel)).First().UserId;
            await SetOwnerForPersonnelAsync(id);
            var list = await _apptRepo.GetByPersonnelAsync(id);

            var now = DateTime.Now;

            var upcoming = list.Where(a => a.AvailableSlot!.Day.ToDateTime(a.AvailableSlot!.EndTime) >= now)
                               .OrderBy(a => a.AvailableSlot!.Day)
                               .ThenBy(a => a.AvailableSlot!.StartTime)
                               .ToList();

            var past = list.Where(a => a.AvailableSlot!.Day.ToDateTime(a.AvailableSlot!.EndTime) < now)
                           .OrderByDescending(a => a.AvailableSlot!.Day)
                           .ThenByDescending(a => a.AvailableSlot!.StartTime)
                           .ToList();

            ViewBag.PersonnelId = id;
            ViewBag.Upcoming = upcoming;
            ViewBag.Past = past;
            return View();
        }
        private async Task SetOwnerForPersonnelAsync(int personnelId)
        {
            var u = await _userRepo.GetAsync(personnelId);
            ViewBag.OwnerName = u?.Name ?? $"Personnel #{personnelId}";
            ViewBag.OwnerRole = "Personnel";
        }

        // Gün seçimi (takvim)
        [HttpGet]
        public IActionResult CreateDay(int personnelId)
        {
            ViewBag.PersonnelId = personnelId;
            return View();
        }

        // Tek gün (day) veya çoklu gün (days = "yyyy-MM-dd,yyyy-MM-dd,...")
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDay(int personnelId, string? days, DateOnly? day)
        {
            // 1) Hedef günleri topla
            var targetDays = new List<DateOnly>();
            if (!string.IsNullOrWhiteSpace(days))
            {
                foreach (var token in days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (DateOnly.TryParse(token, out var d))
                        targetDays.Add(d);
                }
            }
            else if (day.HasValue)
            {
                targetDays.Add(day.Value);
            }

            if (targetDays.Count == 0)
            {
                TempData["Error"] = "Lütfen en az bir gün seçiniz.";
                return RedirectToAction(nameof(CreateDay), new { personnelId });
            }

            // 2) Basit şablon: 3 slot
            var templates = new (TimeOnly Start, TimeOnly End)[]
            {
                (new TimeOnly(9,  0), new TimeOnly(11, 0)),
                (new TimeOnly(12, 0), new TimeOnly(14, 0)),
                (new TimeOnly(16, 0), new TimeOnly(18, 0)),
            };

            int created = 0;

            // 3) Her gün için slotları ekle
            foreach (var d in targetDays.Distinct())
            {
                // Basit kontrol: o günde zaten bu personele ait boş slot var mı?
                // (Not: Tümü doluysa bu kontrol "yok" döner. Tam katı kontrol istersen
                // repo'da "Exists(personnelId, day, start)" gibi bir metod ekle.)
                var existingFree = await _slotRepo.GetFreeSlotsByDayAsync(d);
                if (existingFree.Any(s => s.PersonnelId == personnelId))
                {
                    // Aynı güne bir daha eklemek istemiyorsan skip et
                    continue;
                }

                var toAdd = templates.Select(t => new AvailableSlot
                {
                    PersonnelId = personnelId,
                    Day = d,
                    StartTime = t.Start,
                    EndTime = t.End
                });

                await _slotRepo.AddRangeAsync(toAdd);
                created += 3;
            }

            TempData["Message"] = created > 0
                ? $"Availability saved. ({created} slot)"
                : "Seçilen günlerin hepsi zaten ekli görünüyor.";

            return RedirectToAction(nameof(Dashboard), new { personnelId });
        }
    }
}
