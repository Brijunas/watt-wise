using Microsoft.EntityFrameworkCore;

using WattWise.Infrastructure.Persistence.Schema;

namespace WattWise.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseSchemas.App);
    }
}
