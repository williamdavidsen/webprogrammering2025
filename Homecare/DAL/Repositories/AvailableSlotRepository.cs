using Homecare.DAL.Interfaces;

using Homecare.Models;
using Microsoft.EntityFrameworkCore;

namespace Homecare.DAL.Repositories
{
    public class AvailableSlotRepository : IAvailableSlotRepository
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AvailableSlotRepository> _logger;
        public AvailableSlotRepository(AppDbContext db, ILogger<AvailableSlotRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<AvailableSlot>> GetAllAsync()
        {
            try
            {
                return await _db.AvailableSlots.Include(s => s.Personnel).Include(s => s.Appointment)
               .OrderBy(s => s.Day).ThenBy(s => s.StartTime).ToListAsync();

            }
            catch (Exception e)
            {

                _logger.LogError(e, "[AvaibleSlotRepostory] GetAllAsync failed");
                return new List<AvailableSlot>();
            }
        }


        public async Task<AvailableSlot?> GetAsync(int id)
        {
            try
            {
                return await _db.AvailableSlots.Include(s => s.Personnel).Include(s => s.Appointment)
               .FirstOrDefaultAsync(s => s.AvailableSlotId == id);

            }
            catch (Exception e)
            {

                _logger.LogError(e, "[AvaibleSlotRepostory] GetAsync({Id}) failed", id);
                return null;
            }
        }

        public async Task<List<DateOnly>> GetFreeDaysAsync(int rangeDays = 42)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var until = today.AddDays(rangeDays);

            return await _db.AvailableSlots
                .AsNoTracking()
                .Where(s =>
                    s.Day >= today &&
                    s.Day <= until &&
                    s.Appointment == null)       // boş slot: randevusu yok
                .Select(s => s.Day)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
        }

        public async Task<List<AvailableSlot>> GetFreeSlotsByDayAsync(DateOnly day)
        {
            return await _db.AvailableSlots
                .AsNoTracking()
                .Where(s => s.Day == day && s.Appointment == null)
                .OrderBy(s => s.StartTime)
                .Include(s => s.Personnel)
                .ToListAsync();
        }
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
        public async Task<List<DateOnly>> GetWorkDaysAsync(int personnelId, DateOnly from, DateOnly to)
        {
            return await _db.AvailableSlots
                .Where(s => s.PersonnelId == personnelId &&
                            s.Day >= from && s.Day <= to)
                .Select(s => s.Day)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
        }

        public async Task<List<DateOnly>> GetLockedDaysAsync(int personnelId, DateOnly from, DateOnly to)
        {
            return await _db.AvailableSlots
                .Where(s => s.PersonnelId == personnelId &&
                            s.Day >= from && s.Day <= to &&
                            s.Appointment != null)
                .Select(s => s.Day)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
        }

        public async Task<List<AvailableSlot>> GetSlotsForPersonnelOnDayAsync(int personnelId, DateOnly day)
        {
            return await _db.AvailableSlots
                .Where(s => s.PersonnelId == personnelId && s.Day == day)
                .ToListAsync();
        }



        public async Task RemoveRangeAsync(IEnumerable<AvailableSlot> slots)
        {
            _db.AvailableSlots.RemoveRange(slots);
            await _db.SaveChangesAsync();
        }

        public Task<bool> ExistsAsync(int personnelId, DateOnly day, TimeOnly start, TimeOnly end) =>
            _db.AvailableSlots.AnyAsync(s => s.PersonnelId == personnelId && s.Day == day && s.StartTime == start && s.EndTime == end);
    }
}
