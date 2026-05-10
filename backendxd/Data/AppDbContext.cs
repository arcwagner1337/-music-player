using backendxd.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

namespace backendxd.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<PendingRegistration> PendingRegistrations { get; set; }

        //остальные таблицы добавляются по аналогии
    }
}
