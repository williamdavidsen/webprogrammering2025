using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Homecare.Controllers
{
    public class AppointmentController : Controller
    {
        private readonly IAppointmentRepository _apptRepo;
        private readonly IAvailableSlotRepository _slotRepo;
        private readonly IUserRepository _userRepo;
        private readonly ICareTaskRepository _taskRepo;

        public AppointmentController(
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

        // LIST
        public async Task<IActionResult> Table() => View(await _apptRepo.GetAllAsync());

        // DETAILS
        // AppointmentController.cs
        public async Task<IActionResult> Details(int id, string? backTo = null, int? ownerId = null)
        {
            var a = await _apptRepo.GetAsync(id);
            if (a == null) return NotFound();

            ViewBag.BackTo = backTo;   // "client" / "personnel" / null
            ViewBag.OwnerId = ownerId; // ilgili clientId veya personnelId
            return View(a);
        }


        // CREATE GET
        public async Task<IActionResult> Create()
        {
            // Client seçimi (admin ekranı için)
            ViewBag.Clients = new SelectList(
                await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name");

            // İlk uygun güne göre boş slotlar (klasik dropdown görünümü)
            var freeDays = await _slotRepo.GetFreeDaysAsync();
            var firstDay = freeDays.FirstOrDefault();
            var freeSlots = (firstDay == default)
                ? new List<AvailableSlot>()
                : await _slotRepo.GetFreeSlotsByDayAsync(firstDay);

            ViewBag.FreeSlots = new SelectList(
                freeSlots.Select(s => new
                {
                    s.AvailableSlotId,
                    Label = $"{s.Day:yyyy-MM-dd} {s.StartTime}-{s.EndTime} ({s.Personnel?.Name})"
                }),
                "AvailableSlotId", "Label"
            );

            // Görev dropdown (multi)
            ViewBag.TaskOptions = new MultiSelectList(
                await _taskRepo.GetAllAsync(), "CareTaskId", "Description");

            return View(new Appointment { Status = AppointmentStatus.Scheduled });
        }

        // CREATE POST
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment model, int[]? selectedTaskIds)
        {
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId))
                ModelState.AddModelError(nameof(model.AvailableSlotId), "This slot is already booked.");

            if (!ModelState.IsValid)
            {
                // formu yeniden doldur
                ViewBag.Clients = new SelectList(
                    await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name", model.ClientId);

                // FreeSlots’u tekrar dolduralım (basit: ilk uygun güne göre)
                var freeDays = await _slotRepo.GetFreeDaysAsync();
                var firstDay = freeDays.FirstOrDefault();
                var freeSlots = (firstDay == default)
                    ? new List<AvailableSlot>()
                    : await _slotRepo.GetFreeSlotsByDayAsync(firstDay);

                ViewBag.FreeSlots = new SelectList(
                    freeSlots.Select(s => new
                    {
                        s.AvailableSlotId,
                        Label = $"{s.Day:yyyy-MM-dd} {s.StartTime}-{s.EndTime} ({s.Personnel?.Name})"
                    }),
                    "AvailableSlotId", "Label", model.AvailableSlotId
                );

                ViewBag.TaskOptions = new MultiSelectList(
                    await _taskRepo.GetAllAsync(), "CareTaskId", "Description", selectedTaskIds);

                return View(model);
            }

            await _apptRepo.AddAsync(model);

            // Görev bağlama (varsa)
            if (selectedTaskIds is { Length: > 0 })
                await _apptRepo.ReplaceTasksAsync(model.AppointmentId, selectedTaskIds);

            TempData["Message"] = "Appointment created.";
            return RedirectToAction(nameof(Table));
        }

        // EDIT GET  (takvimli client-edit görünümüyle uyumlu veriler)
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var a = await _apptRepo.GetAsync(id);
            if (a == null) return NotFound();

            // (Eski client dropdown kalsa da sorun olmaz)
            ViewBag.Clients = new SelectList(
                await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name", a.ClientId);

            // Takvim için boş günler (client create/edit ile aynı mantık)
            var freeDays = await _slotRepo.GetFreeDaysAsync();
            ViewBag.FreeDays = freeDays.Select(d => d.ToString("yyyy-MM-dd")).ToList();

            // Görev dropdown (multi) - seçili olanlar işaretli gelsin
            var selectedTaskIds = await _apptRepo.GetTaskIdsAsync(a.AppointmentId);
            ViewBag.TaskOptions = new MultiSelectList(
                await _taskRepo.GetAllAsync(), "CareTaskId", "Description", selectedTaskIds);

            return View(a);
        }

        // EDIT POST
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Appointment model, int[]? selectedTaskIds)
        {
            // Seçilen slot hâlâ uygun mu? (kendi appointment’ı hariç)
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId, model.AppointmentId))
                ModelState.AddModelError(nameof(model.AvailableSlotId), "This slot is already booked.");

            if (!ModelState.IsValid)
            {
                ViewBag.Clients = new SelectList(
                    await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name", model.ClientId);

                var freeDaysRetry = await _slotRepo.GetFreeDaysAsync();
                ViewBag.FreeDays = freeDaysRetry.Select(d => d.ToString("yyyy-MM-dd")).ToList();

                ViewBag.TaskOptions = new MultiSelectList(
                    await _taskRepo.GetAllAsync(), "CareTaskId", "Description", selectedTaskIds);

                return View(model);
            }

            await _apptRepo.UpdateAsync(model);
            await _apptRepo.ReplaceTasksAsync(model.AppointmentId, selectedTaskIds ?? Array.Empty<int>());

            TempData["Message"] = "Appointment updated.";
            return RedirectToAction("Dashboard", "Client", new { clientId = model.ClientId });
        }

        // DELETE GET
        public async Task<IActionResult> Delete(int id)
        {
            var a = await _apptRepo.GetAsync(id);
            if (a == null) return NotFound();
            return View(a);
        }

        // DELETE POST
        [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var a = await _apptRepo.GetAsync(id);
            if (a == null) return NotFound();
            await _apptRepo.DeleteAsync(a);
            TempData["Message"] = "Appointment deleted.";
            return RedirectToAction(nameof(Table));
        }
    }
}
