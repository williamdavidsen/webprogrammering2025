using Homecare.DAL.Interfaces;
using Homecare.Models;
using Microsoft.EntityFrameworkCore;

public class CareTaskRepository : ICareTaskRepository
{
    private readonly AppDbContext _db;
    public CareTaskRepository(AppDbContext db) => _db = db;

    public Task<List<CareTask>> GetAllAsync() =>
        _db.CareTasks.OrderBy(t => t.Description).ToListAsync();

    public Task<CareTask?> GetAsync(int id) =>
        _db.CareTasks.FirstOrDefaultAsync(t => t.CareTaskId == id);
}
