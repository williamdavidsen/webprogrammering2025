namespace Homecare.Models
{
    // Appointment binds a client to one of the 3 slots of an AvailableDay
    public class Appointment
    {
        public int AppointmentId { get; set; }
        public int AvailableDayId { get; set; }       // FK-like (mock for now)
        public int SlotIndex { get; set; }            // 1, 2, or 3
        public string ClientName { get; set; } = string.Empty;
        public string Status { get; set; } = "Scheduled"; // Scheduled/Completed/Cancelled
        public string? Notes { get; set; }
    }
}
