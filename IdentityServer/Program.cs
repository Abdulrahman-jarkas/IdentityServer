using IdentityServer;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up");

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, lc) => lc
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")
        .Enrich.FromLogContext()
        .ReadFrom.Configuration(ctx.Configuration));

    var app = builder
        .ConfigureServices()
        .ConfigurePipeline();

    // Seed database based on arguments
    if (args.Contains("/seed"))
    {
        Log.Information("Seeding database (all environments)...");
        SeedData.EnsureSeedData(app);
        Log.Information("Done seeding database. Exiting.");
        return;
    }

    if (args.Contains("/seed-dev"))
    {
        Log.Information("Seeding database (all environments + dev data)...");
        SeedData.EnsureSeedData(app);
        SeedData.EnsureDevSeedData(app);
        Log.Information("Done seeding database. Exiting.");
        return;
    }

    // Auto-seed in development environment
    if (app.Environment.IsDevelopment())
    {
        Log.Information("Development environment detected. Running auto-seed...");
        SeedData.EnsureSeedData(app);
        SeedData.EnsureDevSeedData(app);
    }

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Unhandled exception");
}
finally
{
    Log.Information("Shut down complete");
    Log.CloseAndFlush();
}