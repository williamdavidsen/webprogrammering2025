using Homecare.Models;

namespace Homecare.DAL.Interfaces
{
    public interface IAvailableSlotRepository
    {
        Task<List<AvailableSlot>> GetAllAsync();
        Task<AvailableSlot?> GetAsync(int id);
        Task<List<DateOnly>> GetFreeDaysAsync(DateOnly? from = null);
        Task<List<AvailableSlot>> GetFreeSlotsByDayAsync(DateOnly day);
        Task AddAsync(AvailableSlot slot);
        Task AddRangeAsync(IEnumerable<AvailableSlot> slots);
        Task UpdateAsync(AvailableSlot slot);
        Task DeleteAsync(AvailableSlot slot);
        Task<bool> ExistsAsync(int personnelId, DateOnly day, TimeOnly start, TimeOnly end);
    }
}
