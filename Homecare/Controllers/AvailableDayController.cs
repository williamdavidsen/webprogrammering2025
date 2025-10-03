using Microsoft.AspNetCore.Mvc;
using Homecare.Models;
using Homecare.ViewModels;

namespace Homecare.Controllers
{
    public class AvailableDayController : Controller
    {
        // Not: Şimdilik mock veri; sonra EF/DB bağlayacağız.
        private List<AvailableDay> GetDays()
        {
            return new List<AvailableDay>
            {
                new() { AvailableDayId = 1, PersonnelName = "Nurse A", Day = DateOnly.FromDateTime(DateTime.Today),       Status = "Open"    },
                new() { AvailableDayId = 2, PersonnelName = "Nurse B", Day = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), Status = "Open"    },
                new() { AvailableDayId = 3, PersonnelName = "Nurse C", Day = DateOnly.FromDateTime(DateTime.Today.AddDays(2)), Status = "Closed"  },
            };
        }

        // /AvailableDay/Table
        public IActionResult Table()
        {
            var vm = new DayListViewModel { Days = GetDays(), CurrentViewName = "Table" };
            return View(vm);
        }

        // /AvailableDay/Grid
        public IActionResult Grid()
        {
            var vm = new DayListViewModel { Days = GetDays(), CurrentViewName = "Grid" };
            return View(vm);
        }

        // /AvailableDay/Details/1
        public IActionResult Details(int id)
        {
            var day = GetDays().FirstOrDefault(d => d.AvailableDayId == id);
            if (day == null) return NotFound();
            return View(day);
        }
    }
}
