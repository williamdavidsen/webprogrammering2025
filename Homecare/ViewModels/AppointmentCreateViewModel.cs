using Homecare.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Collections.Generic;

namespace Homecare.ViewModels
{
    public class AppointmentCreateViewModel
    {
        public Appointment Appointment { get; set; } = new Appointment();

        // Tek seçimlik dropdown için
        public List<SelectListItem> TaskSelectList { get; set; } = new();

        // Kullanıcının seçtiği görev
        public int? SelectedTaskId { get; set; }
    }
}
