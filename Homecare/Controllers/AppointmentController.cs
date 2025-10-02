using Microsoft.AspNetCore.Mvc;
using Homecare.Models;

namespace Homecare.Controllers
{
    public class AppointmentController : Controller
    {
        public IActionResult Table()
        {
            // Assume day #1 has slot 1 and 2 booked, slot 3 empty
            var appts = new List<Appointment>
            {
                new() { AppointmentId=1, AvailableDayId=1, SlotIndex=1, ClientName="Client Ali", Status="Scheduled", Notes="Medication reminder" },
                new() { AppointmentId=2, AvailableDayId=1, SlotIndex=2, ClientName="Client Eva", Status="Scheduled", Notes="Grocery assistance" }
            };

            return View(appts);
        }
    }
}
