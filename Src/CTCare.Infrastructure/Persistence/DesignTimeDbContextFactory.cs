using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CTCare.Infrastructure.Persistence;

public class DesignTimeDbContextFactory: IDesignTimeDbContextFactory<CtCareDbContext>
{
    public CtCareDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../CtCare.API");

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CtCareDbContext>();
        optionsBuilder.UseNpgsql(config.GetConnectionString("DefaultConnection"), npg => npg.MigrationsAssembly(typeof(CtCareDbContext).Assembly.FullName));

        return new CtCareDbContext(optionsBuilder.Options);
    }
}
