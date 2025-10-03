namespace Homecare.ViewModels
{
    using Homecare.Models;
    public class DayListViewModel
    {
        public IEnumerable<AvailableDay> Days { get; set; } = new List<AvailableDay>();
        public string CurrentViewName { get; set; } = string.Empty;
    }
}
