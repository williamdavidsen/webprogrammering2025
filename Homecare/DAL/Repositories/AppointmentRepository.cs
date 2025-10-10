using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.EntityFrameworkCore;

namespace Homecare.DAL.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly AppDbContext _db;
        public AppointmentRepository(AppDbContext db) => _db = db;

        public Task<List<Appointment>> GetAllAsync() =>
            _db.Appointments
               .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Include(a => a.Client)
               .AsNoTracking()
               .OrderBy(a => a.AvailableSlot!.Day).ThenBy(a => a.AvailableSlot!.StartTime)
               .ToListAsync();

        public Task<Appointment?> GetAsync(int id) =>
            _db.Appointments
               .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Include(a => a.Client)
               .AsNoTracking()
               .FirstOrDefaultAsync(a => a.AppointmentId == id);

        public Task<List<Appointment>> GetByClientAsync(int clientId) =>
            _db.Appointments
               .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Where(a => a.ClientId == clientId)
               .AsNoTracking()
               .ToListAsync();

        public Task<List<Appointment>> GetByPersonnelAsync(int personnelId) =>
            _db.Appointments
               .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Where(a => a.AvailableSlot!.PersonnelId == personnelId)
               .AsNoTracking()
               .ToListAsync();

        // ← İMZA BURADA
        public async Task<bool> SlotIsBookedAsync(int availableSlotId, int? ignoreAppointmentId = null)
        {
            var q = _db.Appointments.AsNoTracking()
                     .Where(a => a.AvailableSlotId == availableSlotId);

            if (ignoreAppointmentId.HasValue)
                q = q.Where(a => a.AppointmentId != ignoreAppointmentId.Value);

            return await q.AnyAsync();
        }

        public async Task AddAsync(Appointment a)
        {
            _db.Appointments.Add(a);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateAsync(Appointment a)
        {
            _db.Appointments.Update(a);
            await _db.SaveChangesAsync();
        }

        public async Task DeleteAsync(Appointment a)
        {
            _db.Appointments.Remove(a);
            await _db.SaveChangesAsync();
        }

        public Task<int[]> GetTaskIdsAsync(int appointmentId) =>
            _db.TaskLists
               .Where(t => t.AppointmentId == appointmentId)
               .Select(t => t.CareTaskId)
               .ToArrayAsync();

        public async Task ReplaceTasksAsync(int appointmentId, IEnumerable<int> careTaskIds)
        {
            var existing = _db.TaskLists.Where(t => t.AppointmentId == appointmentId);
            _db.TaskLists.RemoveRange(existing);

            foreach (var id in careTaskIds.Distinct())
                _db.TaskLists.Add(new TaskList { AppointmentId = appointmentId, CareTaskId = id });

            await _db.SaveChangesAsync();
        }
    }
}
