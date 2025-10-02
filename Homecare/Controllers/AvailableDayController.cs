using Microsoft.AspNetCore.Mvc;
using Homecare.Models;

namespace Homecare.Controllers
{
    public class AvailableDayController : Controller
    {
        public IActionResult Table()
        {
            var days = new List<AvailableDay>
            {
                new() { AvailableDayId = 1, PersonnelName = "Nurse A", Day = DateOnly.FromDateTime(DateTime.Today), Status="Open" },
                new() { AvailableDayId = 2, PersonnelName = "Nurse B", Day = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), Status="Open" },
                new() { AvailableDayId = 3, PersonnelName = "Nurse C", Day = DateOnly.FromDateTime(DateTime.Today.AddDays(2)), Status="Closed" },
            };

            return View(days);
        }
    }
}
