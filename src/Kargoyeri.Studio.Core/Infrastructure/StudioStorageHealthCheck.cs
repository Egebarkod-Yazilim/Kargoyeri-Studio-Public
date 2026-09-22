using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kargoyeri.Studio.Core.Infrastructure;

internal sealed class StudioStorageHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public StudioStorageHealthCheck(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var basePath = _configuration["Kargoyeri:Storage:BasePath"]
                ?? _configuration["Storage:BasePath"]
                ?? "data";

            var resolvedPath = Path.IsPathRooted(basePath)
                ? basePath
                : Path.Combine(_environment.ContentRootPath, basePath);

            if (!Directory.Exists(resolvedPath))
            {
                Directory.CreateDirectory(resolvedPath);
            }

            // Yazma testi: gecici bir dosya olustur ve sil
            var testFile = Path.Combine(resolvedPath, ".healthcheck");
            File.WriteAllText(testFile, DateTime.UtcNow.ToString("O"));
            File.Delete(testFile);

            return Task.FromResult(HealthCheckResult.Healthy($"Depolama erisimi OK: {resolvedPath}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy($"Depolama erisimi basarisiz: {ex.Message}"));
        }
    }
}
