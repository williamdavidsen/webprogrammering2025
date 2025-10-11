using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.EntityFrameworkCore;
using System;

namespace Homecare.DAL.Repositories
{
    public class AppointmentRepository : IAppointmentRepository
    {
        private readonly AppDbContext _db;
        private readonly ILogger<AppointmentRepository> _logger;
        public AppointmentRepository(AppDbContext db, ILogger<AppointmentRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<List<Appointment>> GetAllAsync()

        {
            try
            {
                return await _db.Appointments
              .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
              .Include(a => a.Client)
              .AsNoTracking()
              .OrderBy(a => a.AvailableSlot!.Day).ThenBy(a => a.AvailableSlot!.StartTime)
              .ToListAsync();

            }
            catch (Exception e)
            {

                _logger.LogError(e, "[AppointmentRepository] GetAllAsync failed");
                return new List<Appointment>();
            }
        }

        public async Task<Appointment?> GetAsync(int id)

        {
            try
            {
                return await _db.Appointments
               .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Include(a => a.Client)
               .AsNoTracking()
               .FirstOrDefaultAsync(a => a.AppointmentId == id);
            }
            catch (Exception e)
            {

                _logger.LogError(e, "[AppointmentRepository] GetAsync({Id}) failed", id);
                return null;
            }
        }

        public async Task<List<Appointment>> GetByClientAsync(int clientId)
        {
            try
            {
                return await _db.Appointments
               .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Where(a => a.ClientId == clientId)
               .AsNoTracking()
               .ToListAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "[AppointmentRepository] GetByClientAsync({ClientId}) failed", clientId);
                return new List<Appointment>();

            }
        }

        public async Task<List<Appointment>> GetByPersonnelAsync(int personnelId)
        {
            try
            {
                return await _db.Appointments
               .Include(a => a.AvailableSlot)!.ThenInclude(s => s.Personnel)
               .Where(a => a.AvailableSlot!.PersonnelId == personnelId)
               .AsNoTracking()
               .ToListAsync();

            }
            catch (Exception e)
            {

                _logger.LogError(e, "[AppointmentRepository] GetByPersonnelAsync({PersonnelId}) failed", personnelId);
                return new List<Appointment>();
            }
        }
        // ← İMZA BURADA
        public async Task<bool> SlotIsBookedAsync(int availableSlotId, int? ignoreId = null)
        {
            try
            {
                return await _db.Appointments
                    .AnyAsync(a => a.AvailableSlotId == availableSlotId
                                   && (ignoreId == null || a.AppointmentId != ignoreId.Value));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentRepository] SlotIsBookedAsync({SlotId},{Ignore}) failed",
                    availableSlotId, ignoreId);
                // temkinli davran: bilinmiyorsa 'booked' varsay
                return true;
            }
        }

        public async Task AddAsync(Appointment a)
        {
            try
            {
                _db.Appointments.Add(a);
                await _db.SaveChangesAsync();
                _logger.LogInformation("[AppointmentRepository] Added appointment #{Id} for client #{ClientId}", a.AppointmentId, a.ClientId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentRepository] AddAsync failed for client #{ClientId}", a.ClientId);
                throw; // üst katman yakalasın
            }
        }

        public async Task UpdateAsync(Appointment a)
        {
            try
            {
                _db.Appointments.Update(a);
                await _db.SaveChangesAsync();
                _logger.LogInformation("[AppointmentRepository] Updated appointment #{Id}", a.AppointmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentRepository] UpdateAsync failed for #{Id}", a.AppointmentId);
                throw;
            }
        }

        public async Task DeleteAsync(Appointment a)
        {
            try
            {
                _db.Appointments.Remove(a);
                await _db.SaveChangesAsync();
                _logger.LogInformation("[AppointmentRepository] Deleted appointment #{Id}", a.AppointmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentRepository] DeleteAsync failed for #{Id}", a.AppointmentId);
                throw;
            }
        }

        public async Task<int[]> GetTaskIdsAsync(int appointmentId)
        {
            try
            {
                return await _db.TaskLists
                    .Where(t => t.AppointmentId == appointmentId)
                    .Select(t => t.CareTaskId)
                    .ToArrayAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentRepository] GetTaskIdsAsync({Id}) failed", appointmentId);
                return Array.Empty<int>();
            }
        }

        public async Task ReplaceTasksAsync(int appointmentId, IEnumerable<int> careTaskIds)
        {
            try
            {
                var existing = _db.TaskLists.Where(x => x.AppointmentId == appointmentId);
                _db.TaskLists.RemoveRange(existing);

                var add = careTaskIds.Distinct().Select(id => new TaskList
                {
                    AppointmentId = appointmentId,
                    CareTaskId = id
                });
                await _db.TaskLists.AddRangeAsync(add);
                await _db.SaveChangesAsync();

                _logger.LogInformation("[AppointmentRepository] Replaced tasks for appointment #{Id}", appointmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AppointmentRepository] ReplaceTasksAsync({Id}) failed", appointmentId);
                throw;
            }
        }
    }
}
