using Homecare.DAL.Interfaces;

using Homecare.Models;
using Microsoft.EntityFrameworkCore;

namespace Homecare.DAL.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;
        public UserRepository(AppDbContext db) => _db = db;

        public Task<User?> GetAsync(int id) => _db.Users.FirstOrDefaultAsync(u => u.UserId == id);
        public Task<List<User>> GetByRoleAsync(UserRole role) => _db.Users.Where(u => u.Role == role).ToListAsync();
    }
}
