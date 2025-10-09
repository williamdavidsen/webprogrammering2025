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

        public AppointmentController(IAppointmentRepository apptRepo, IAvailableSlotRepository slotRepo, IUserRepository userRepo)
        {
            _apptRepo = apptRepo;
            _slotRepo = slotRepo;
            _userRepo = userRepo;
        }

        // LIST
        public async Task<IActionResult> Table() => View(await _apptRepo.GetAllAsync());

        // DETAILS
        public async Task<IActionResult> Details(int id)
        {
            var a = await _apptRepo.GetAsync(id);
            if (a == null) return NotFound();
            return View(a);
        }

        // CREATE GET
        public async Task<IActionResult> Create()
        {
            ViewBag.Clients = new SelectList(await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name");

            var freeDays = await _slotRepo.GetFreeDaysAsync();
            var firstDay = freeDays.FirstOrDefault();
            var freeSlots = (firstDay == default)
                ? new List<AvailableSlot>()
                : await _slotRepo.GetFreeSlotsByDayAsync(firstDay);

            ViewBag.FreeSlots = new SelectList(
                freeSlots.Select(s => new { s.AvailableSlotId, Label = $"{s.Day:yyyy-MM-dd} {s.StartTime}-{s.EndTime} ({s.Personnel?.Name})" }),
                "AvailableSlotId", "Label"
            );

            return View(new Appointment { Status = AppointmentStatus.Scheduled });
        }

        // CREATE POST
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Appointment model)
        {
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId))
            {
                ModelState.AddModelError(nameof(model.AvailableSlotId), "This slot is already booked.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Clients = new SelectList(await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name", model.ClientId);
                return View(model);
            }

            await _apptRepo.AddAsync(model);
            TempData["Message"] = "Appointment created.";
            return RedirectToAction(nameof(Table));
        }

        // EDIT GET  ✅ FreeDays eklendi
        public async Task<IActionResult> Edit(int id)
        {
            var a = await _apptRepo.GetAsync(id);
            if (a == null) return NotFound();

            // (Eski görünümde client dropdown vardı; görünüm artık hidden kullanıyor ama aşağıdaki satır kalsa da zarar vermez)
            ViewBag.Clients = new SelectList(await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name", a.ClientId);

            // Takvim için boş günler (ClientController.Create ile aynı mantık)
            var freeDays = await _slotRepo.GetFreeDaysAsync(); // List<DateOnly>
            ViewBag.FreeDays = freeDays.Select(d => d.ToString("yyyy-MM-dd")).ToList();

            return View(a);
        }

        // EDIT POST  ✅ Hata dönüşünde FreeDays yenile
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Appointment model)
        {
            if (await _apptRepo.SlotIsBookedAsync(model.AvailableSlotId, model.AppointmentId))
            {
                ModelState.AddModelError(nameof(model.AvailableSlotId), "This slot is already booked.");

                ViewBag.Clients = new SelectList(await _userRepo.GetByRoleAsync(UserRole.Client), "UserId", "Name", model.ClientId);
                var freeDaysRetry = await _slotRepo.GetFreeDaysAsync();
                ViewBag.FreeDays = freeDaysRetry.Select(d => d.ToString("yyyy-MM-dd")).ToList();

                return View(model);
            }

            await _apptRepo.UpdateAsync(model);
            TempData["Message"] = "Appointment updated.";
            return RedirectToAction(nameof(Table));
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
