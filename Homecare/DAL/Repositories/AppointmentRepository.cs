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
            _db.Appointments.Include(a => a.Client)
                .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
                .OrderBy(a => a.AvailableSlot!.Day).ThenBy(a => a.AvailableSlot!.StartTime)
                .ToListAsync();

        public Task<Appointment?> GetAsync(int id) =>
            _db.Appointments.Include(a => a.Client)
                .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
                .Include(a => a.Tasks).ThenInclude(t => t.CareTask)
                .FirstOrDefaultAsync(a => a.AppointmentId == id);

        public Task<List<Appointment>> GetByClientAsync(int clientId) =>
            _db.Appointments.Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Where(a => a.ClientId == clientId).ToListAsync();

        public Task<List<Appointment>> GetByPersonnelAsync(int personnelId) =>
            _db.Appointments.Include(a => a.Client).Include(a => a.AvailableSlot)
               .Where(a => a.AvailableSlot!.PersonnelId == personnelId).ToListAsync();

        public Task<bool> SlotIsBookedAsync(int availableSlotId, int? ignoreId = null) =>
            _db.Appointments.AnyAsync(a => a.AvailableSlotId == availableSlotId &&
                                           (!ignoreId.HasValue || a.AppointmentId != ignoreId.Value));

        public async Task AddAsync(Appointment a) { _db.Appointments.Add(a); await _db.SaveChangesAsync(); }
        public async Task UpdateAsync(Appointment a) { _db.Appointments.Update(a); await _db.SaveChangesAsync(); }
        public async Task DeleteAsync(Appointment a) { _db.Appointments.Remove(a); await _db.SaveChangesAsync(); }
    }
}
