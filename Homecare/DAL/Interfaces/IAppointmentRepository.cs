using Homecare.Models;

namespace Homecare.DAL.Interfaces
{
    public interface IAppointmentRepository
    {
        Task<List<Appointment>> GetAllAsync();
        Task<Appointment?> GetAsync(int id);
        Task<List<Appointment>> GetByClientAsync(int clientId);
        Task<List<Appointment>> GetByPersonnelAsync(int personnelId);
        Task<bool> SlotIsBookedAsync(int availableSlotId, int? ignoreId = null);
        Task AddAsync(Appointment a);
        Task UpdateAsync(Appointment a);
        Task DeleteAsync(Appointment a);
    }
}
