using EnergyAutomate.Definitions;
using Growatt.OSS;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EnergyAutomate.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    #region Fields

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions();

    #endregion Fields

    #region Public Constructors

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    #endregion Public Constructors

    #region Properties

    public DbSet<DeviceNoahInfo> DeviceNoahInfo { get; set; }
    public DbSet<DeviceNoahLastData> DeviceNoahLastData { get; set; }
    public DbSet<DeviceList> Devices { get; set; }
    public DbSet<TibberPrice> Prices { get; set; }
    public DbSet<RealTimeMeasurementExtention> RealTimeMeasurements { get; set; }

    #endregion Properties

    #region Protected Methods

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeviceList>().HasKey(x => x.DeviceSn);
        modelBuilder.Entity<DeviceNoahInfo>().HasKey(x => x.DeviceSn);
        modelBuilder.Entity<DeviceNoahInfo>().Ignore(x => x.TimeSegments);
        modelBuilder.Entity<DeviceNoahLastData>().HasKey(x => new { x.deviceSn, x.time });

        modelBuilder.Entity<RealTimeMeasurementExtention>()
            .HasKey(x => new { x.Timestamp });

        modelBuilder.Entity<TibberPrice>().HasKey(x => new { x.StartsAt });

        // Set all string properties to be nullable
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(string) && !property.IsPrimaryKey())
                {
                    property.IsNullable = true;
                }
            }
        }

        // other configurations
    }

    #endregion Protected Methods
}
