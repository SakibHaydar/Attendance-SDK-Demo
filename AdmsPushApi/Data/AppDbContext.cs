using Microsoft.EntityFrameworkCore;
using AdmsPushApi.Models;

namespace AdmsPushApi.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
    }
}
