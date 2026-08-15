using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Xtzkt.Data;

namespace Xtzkt.Indexers.Common.Utils;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<XtzktContext>
{
    public XtzktContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(
                $"appsettings.json",
                optional: false,
                reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var builder = new DbContextOptionsBuilder<XtzktContext>();
        builder.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));

        return new XtzktContext(builder.Options);
    }
}
