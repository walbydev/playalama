using Microsoft.EntityFrameworkCore;

namespace Lama.Server.Data;

public sealed class LamaDbContext(DbContextOptions<LamaDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Phase 1: only the DbContext and provider wiring are introduced.
        // Entity sets and mappings are added in Phase 2.
        base.OnModelCreating(modelBuilder);
    }
}

