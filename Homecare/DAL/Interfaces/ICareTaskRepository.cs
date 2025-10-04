using Homecare.Models;

namespace Homecare.DAL.Interfaces
{
    public interface ICareTaskRepository
    {
        Task<List<CareTask>> GetAllAsync();
    }
}
