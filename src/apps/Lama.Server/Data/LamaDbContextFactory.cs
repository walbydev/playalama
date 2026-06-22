using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Lama.Server.Data;

public sealed class LamaDbContextFactory : IDesignTimeDbContextFactory<LamaDbContext>
{
    public LamaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<LamaDbContext>();

        var connectionString =
            Environment.GetEnvironmentVariable("LAMA_SERVER_DB")
            ?? "Host=localhost;Port=5200;Database=lama_dev;Username=lama_dev;Password=dev_password_change_me;Ssl Mode=Disable;";

        optionsBuilder.UseNpgsql(connectionString);

        return new LamaDbContext(optionsBuilder.Options);
    }
}

