using Homecare.DAL.Interfaces;

using Homecare.Models;
using Microsoft.EntityFrameworkCore;

namespace Homecare.DAL.Repositories
{
    public class AvailableSlotRepository : IAvailableSlotRepository
    {
        private readonly AppDbContext _db;
        public AvailableSlotRepository(AppDbContext db) => _db = db;

        public Task<List<AvailableSlot>> GetAllAsync() =>
            _db.AvailableSlots.Include(s => s.Personnel).Include(s => s.Appointment)
               .OrderBy(s => s.Day).ThenBy(s => s.StartTime).ToListAsync();

        public Task<AvailableSlot?> GetAsync(int id) =>
            _db.AvailableSlots.Include(s => s.Personnel).Include(s => s.Appointment)
               .FirstOrDefaultAsync(s => s.AvailableSlotId == id);

        public async Task<List<DateOnly>> GetFreeDaysAsync(DateOnly? from = null)
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.Today);
            return await _db.AvailableSlots.Include(s => s.Appointment)
                .Where(s => s.Appointment == null && s.Day >= start)
                .Select(s => s.Day).Distinct().OrderBy(d => d).ToListAsync();
        }

        public Task<List<AvailableSlot>> GetFreeSlotsByDayAsync(DateOnly day) =>
            _db.AvailableSlots.Include(s => s.Personnel).Include(s => s.Appointment)
               .Where(s => s.Day == day && s.Appointment == null)
               .OrderBy(s => s.StartTime).ToListAsync();

        public async Task AddAsync(AvailableSlot slot)
        {
            _db.AvailableSlots.Add(slot);
            await _db.SaveChangesAsync();
        }
        public async Task AddRangeAsync(IEnumerable<AvailableSlot> slots)
        {
            _db.AvailableSlots.AddRange(slots);
            await _db.SaveChangesAsync();
        }
        public async Task UpdateAsync(AvailableSlot slot)
        {
            _db.AvailableSlots.Update(slot);
            await _db.SaveChangesAsync();
        }
        public async Task DeleteAsync(AvailableSlot slot)
        {
            _db.AvailableSlots.Remove(slot);
            await _db.SaveChangesAsync();
        }

        public Task<bool> ExistsAsync(int personnelId, DateOnly day, TimeOnly start, TimeOnly end) =>
            _db.AvailableSlots.AnyAsync(s => s.PersonnelId == personnelId && s.Day == day && s.StartTime == start && s.EndTime == end);
    }
}
