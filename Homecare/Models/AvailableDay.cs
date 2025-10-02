namespace Homecare.Models
{
    // Nurse chooses a day; the day has 3 fixed slots (1..3)
    public class AvailableDay
    {
        public int AvailableDayId { get; set; }
        public string PersonnelName { get; set; } = string.Empty;
        public DateOnly Day { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        public string Status { get; set; } = "Open"; // Open / Closed
    }
}
