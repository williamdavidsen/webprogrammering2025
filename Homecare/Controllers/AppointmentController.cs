using Homecare.DAL.Interfaces;
using Homecare.Models;
using Homecare.ViewModels;
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

            // Takvim için boş günler
            var freeDays = await _slotRepo.GetFreeDaysAsync(); // List<DateOnly>
            ViewBag.FreeDays = freeDays.Select(d => d.ToString("yyyy-MM-dd")).ToList();

            // Mevcut randevudaki (varsa) ilk görev id’si
            var currentTaskIds = await _apptRepo.GetTaskIdsAsync(id);
            int? selectedTaskId = currentTaskIds.FirstOrDefault(); // yoksa 0 döner, null’a çevireceğiz

            // Dropdown seçenekleri
            var allTasks = await _taskRepo.GetAllAsync();
            var selectList = allTasks.Select(t => new SelectListItem
            {
                Value = t.CareTaskId.ToString(),
                Text = t.Description
            });

            var vm = new AppointmentEditViewModel
            {
                Appointment = a,
                SelectedTaskId = (selectedTaskId == 0 ? null : selectedTaskId),
                TaskSelectList = selectList
            };

            return View(vm);
        }

        // ================== EDIT (POST) ==================
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(AppointmentEditViewModel vm)
        {
            var model = vm.Appointment;

            // Slot hâlâ uygun mu? (ignore: kendisi)
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId, model.AppointmentId))
            {
                ModelState.AddModelError(nameof(model.AvailableSlotId), "This slot is already booked.");
            }

            if (!ModelState.IsValid)
            {
                // Hata durumunda takvim & dropdown yenile
                var freeDaysRetry = await _slotRepo.GetFreeDaysAsync();
                ViewBag.FreeDays = freeDaysRetry.Select(d => d.ToString("yyyy-MM-dd")).ToList();

                var allTasks = await _taskRepo.GetAllAsync();
                vm.TaskSelectList = allTasks.Select(t => new SelectListItem
                {
                    Value = t.CareTaskId.ToString(),
                    Text = t.Description,
                    Selected = (vm.SelectedTaskId.HasValue && vm.SelectedTaskId.Value == t.CareTaskId)
                });

                return View(vm);
            }

            await _apptRepo.UpdateAsync(model);

            // Dropdown tek seçim: varsa 1 görev, yoksa boşalt
            if (vm.SelectedTaskId.HasValue)
                await _apptRepo.ReplaceTasksAsync(model.AppointmentId, new[] { vm.SelectedTaskId.Value });
            else
                await _apptRepo.ReplaceTasksAsync(model.AppointmentId, Array.Empty<int>());

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
