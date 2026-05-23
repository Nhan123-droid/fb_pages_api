using Microsoft.EntityFrameworkCore;
using Page_API.Models;

namespace Page_API.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ProcessedCommand> ProcessedCommands { get; set; }
    }
}
