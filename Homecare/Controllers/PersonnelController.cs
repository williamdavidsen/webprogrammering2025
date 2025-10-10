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
                               .OrderBy(a => a.AvailableSlot!.Day).ThenBy(a => a.AvailableSlot!.StartTime).ToList();

            var past = list.Where(a => a.AvailableSlot!.Day.ToDateTime(a.AvailableSlot!.EndTime) < now)
                           .OrderByDescending(a => a.AvailableSlot!.Day).ThenByDescending(a => a.AvailableSlot!.StartTime).ToList();

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
        public async Task<IActionResult> CreateDay(int personnelId)
        {
            await SetOwnerForPersonnelAsync(personnelId);
            ViewBag.PersonnelId = personnelId;

            var from = DateOnly.FromDateTime(DateTime.Today);
            var to = from.AddDays(42); // 6 hafta

            // Takvim açıldığında mavi olsun (çalışacağı günler)
            var selectedDays = await _slotRepo.GetWorkDaysAsync(personnelId, from, to);
            // Üzerinde randevu olan günler (kaldırılamaz)
            var lockedDays = await _slotRepo.GetLockedDaysAsync(personnelId, from, to);

            ViewBag.SelectedDaysJson = System.Text.Json.JsonSerializer.Serialize(
                selectedDays.Select(d => d.ToString("yyyy-MM-dd")));

            ViewBag.LockedDaysJson = System.Text.Json.JsonSerializer.Serialize(
                lockedDays.Select(d => d.ToString("yyyy-MM-dd")));

            return View(); // CreateDay.cshtml
        }

        // ----- CREATE DAY (POST) => Seçimi uygula -----
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDay(int personnelId, string? days)
        {
            // Görüntülediğimiz aralık: bugün + 6 hafta
            var from = DateOnly.FromDateTime(DateTime.Today);
            var to = from.AddDays(42);

            // Repo null dönerse boş koleksiyonla devam et → ToHashSet patlamaz
            var existingDays = (await _slotRepo.GetWorkDaysAsync(personnelId, from, to)
                                ?? Enumerable.Empty<DateOnly>()).ToHashSet();

            var lockedDays = (await _slotRepo.GetLockedDaysAsync(personnelId, from, to)
                                ?? Enumerable.Empty<DateOnly>()).ToHashSet();

            // Formdan gelen seçimleri topla (CSV: "yyyy-MM-dd,yyyy-MM-dd,...")
            var chosen = new HashSet<DateOnly>();
            if (!string.IsNullOrWhiteSpace(days))
            {
                foreach (var s in days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    if (DateOnly.TryParse(s, out var d))
                        chosen.Add(d);
            }

            // Ekle/sil kümeleri
            var toAdd = chosen.Except(existingDays).ToList();
            var toRemove = existingDays.Except(chosen).ToList();

            // Randevusu olan günler kaldırılamaz
            var blocked = toRemove.Where(d => lockedDays.Contains(d)).ToList();
            var removable = toRemove.Where(d => !lockedDays.Contains(d)).ToList();

            // --- 1) Eklenecek günlere 3 preset slot ekle ---
            var presets = new (TimeOnly Start, TimeOnly End)[]
            {
        (new TimeOnly(9,  0), new TimeOnly(11, 0)),
        (new TimeOnly(12, 0), new TimeOnly(14, 0)),
        (new TimeOnly(16, 0), new TimeOnly(18, 0))
            };

            if (toAdd.Count > 0)
            {
                var newSlots = new List<AvailableSlot>(toAdd.Count * presets.Length);
                foreach (var day in toAdd)
                    foreach (var p in presets)
                        newSlots.Add(new AvailableSlot
                        {
                            PersonnelId = personnelId,
                            Day = day,
                            StartTime = p.Start,
                            EndTime = p.End
                        });

                await _slotRepo.AddRangeAsync(newSlots);
            }

            // --- 2) Randevusu olmayan günlerin slotlarını sil ---
            if (removable.Count > 0)
            {
                foreach (var day in removable)
                {
                    var slots = await _slotRepo.GetSlotsForPersonnelOnDayAsync(personnelId, day)
                                ?? Enumerable.Empty<AvailableSlot>();

                    // Güvenlik için tekrar kontrol: listede randevulu slot varsa bu günü de "blocked"a at
                    if (slots.Any(s => s.Appointment != null))
                    {
                        if (!blocked.Contains(day))
                            blocked.Add(day);
                        continue;
                    }

                    if (slots.Any())
                        await _slotRepo.RemoveRangeAsync(slots);
                }
            }

            // --- Kullanıcı mesajı ---
            if (blocked.Count > 0)
            {
                TempData["Error"] =
                    "Şu günlerde randevu olduğu için kaldırılamadı: " +
                    string.Join(", ", blocked.OrderBy(d => d).Select(d => d.ToString("yyyy-MM-dd"))) +
                    ". Lütfen yönetimle iletişime geçiniz.";
            }
            else
            {
                var msg = (toAdd.Count, removable.Count) switch
                {
                    ( > 0, > 0) => $"{toAdd.Count} gün eklendi, {removable.Count} gün kaldırıldı.",
                    ( > 0, 0) => $"{toAdd.Count} gün eklendi.",
                    (0, > 0) => $"{removable.Count} gün kaldırıldı.",
                    _ => "Herhangi bir değişiklik yok."
                };
                TempData["Message"] = msg;
            }

            return RedirectToAction(nameof(Dashboard), new { personnelId });
        }

    }
}
