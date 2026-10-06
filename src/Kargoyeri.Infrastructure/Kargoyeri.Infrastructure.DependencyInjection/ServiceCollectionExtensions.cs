using System;
using System.IO;
using System.Net.Http;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Application.Options;
using Kargoyeri.Application.Services;
using Kargoyeri.Infrastructure.Persistence;
using Kargoyeri.Infrastructure.Persistence.SqlServer;
using Kargoyeri.Infrastructure.Providers;
using Kargoyeri.Infrastructure.Providers.Blueprints;
using Kargoyeri.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
	public static IServiceCollection AddKargoyeriCore(this IServiceCollection services, IConfiguration configuration, string contentRootPath)
	{
		services.Configure<StorageOptions>(configuration.GetSection("Storage"));
		services.Configure<ProcessingOptions>(configuration.GetSection("Processing"));
		services.Configure<BootstrapCustomersOptions>(configuration.GetSection("BootstrapCustomers"));
		services.Configure<AdminApiOptions>(configuration.GetSection("AdminApi"));
		services.Configure<SmtpOptions>(configuration.GetSection("Smtp"));
		services.Configure<NetgsmOptions>(configuration.GetSection("Netgsm"));
		RegisterPersistence(services, configuration, contentRootPath);
		RegisterHttpClients(services, configuration);
		services.AddSingleton<IProviderBlueprint, SandboxProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, MngProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, ArasProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, UpsProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, SuratProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, YurticiProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, PttProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, HepsiJetProviderBlueprint>();
		services.AddSingleton<IProviderBlueprint, TrendyolExpressProviderBlueprint>();
		services.AddSingleton<ICargoProvider, SandboxCargoProvider>();
		services.AddSingleton<ICargoProvider, MngCargoProvider>();
		services.AddSingleton<ICargoProvider, ArasCargoProvider>();
		services.AddSingleton<ICargoProvider, UpsCargoProvider>();
		services.AddSingleton<ICargoProvider, SuratCargoProvider>();
		services.AddSingleton<ICargoProvider, YurticiCargoProvider>();
		services.AddSingleton<ICargoProvider, PttCargoProvider>();
		services.AddSingleton<ICargoProvider, HepsiJetCargoProvider>();
		services.AddSingleton<ICargoProvider, TrendyolExpressCargoProvider>();
		services.AddSingleton<ICargoProviderResolver, CargoProviderResolver>();
		services.AddScoped<INotificationDispatcher, CompositeNotificationDispatcher>();
		services.AddScoped<CustomerService>();
		services.AddScoped<NotificationService>();
		services.AddScoped<ProviderCatalogService>();
		services.AddScoped<ProviderIntegrationStatusService>();
		services.AddScoped<ApiDocumentationService>();
		services.AddScoped<TenantOnboardingService>();
		services.AddScoped((IServiceProvider sp) => sp.GetRequiredService<IOptions<BootstrapCustomersOptions>>().Value);
		services.AddScoped<CustomerBootstrapper>();
		services.AddScoped<CargoOrchestrator>();
		return services;
	}

	public static IServiceCollection AddKargoyeriApiKeyAuth(this IServiceCollection services)
	{
		services.AddAuthentication("ApiKey").AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", delegate
		{
		});
		services.AddAuthorization();
		return services;
	}

	private static void RegisterHttpClients(IServiceCollection services, IConfiguration configuration)
	{
		ProcessingOptions processingOptions = configuration.GetSection("Processing").Get<ProcessingOptions>() ?? new ProcessingOptions();
		TimeSpan timeout = TimeSpan.FromSeconds(Math.Max(10, processingOptions.ProviderTimeoutSeconds));
		services.AddHttpClient(string.Empty, delegate(HttpClient client)
		{
			client.Timeout = timeout;
		});
		services.AddHttpClient("CompositeNotificationDispatcher", delegate(HttpClient client)
		{
			client.Timeout = TimeSpan.FromSeconds(15.0);
		}).AddHttpMessageHandler(() => new WebhookRetryHandler());
	}

	private static void RegisterPersistence(IServiceCollection services, IConfiguration configuration, string contentRootPath)
	{
		StorageOptions storageOptions = configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
		if (string.Equals(storageOptions.Mode, "InMemory", StringComparison.OrdinalIgnoreCase))
		{
			services.AddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
			services.AddSingleton<IShipmentRepository, InMemoryShipmentRepository>();
			services.AddSingleton<IOperationLogRepository, InMemoryOperationLogRepository>();
			services.AddSingleton<INotificationRepository, InMemoryNotificationRepository>();
			services.AddSingleton<IProviderSettingsRepository, InMemoryProviderSettingsRepository>();
			services.AddScoped<IUnitOfWork, NoOpUnitOfWork>();
		}
		else if (string.Equals(storageOptions.Mode, "SqlServer", StringComparison.OrdinalIgnoreCase))
		{
			string connectionString = storageOptions.ConnectionString ?? configuration.GetConnectionString("Kargoyeri") ?? throw new InvalidOperationException("SQL Server modu secildi fakat baglanti dizesi bulunamadi. Lutfen 'Storage:ConnectionString' veya 'ConnectionStrings:Kargoyeri' ayarini yapilandirin.");
			EntityFrameworkServiceCollectionExtensions.AddDbContext<KargoyeriDbContext>(services, (Action<DbContextOptionsBuilder>)delegate(DbContextOptionsBuilder options)
			{
				SqlServerDbContextOptionsExtensions.UseSqlServer(options, connectionString, (Action<SqlServerDbContextOptionsBuilder>)delegate(SqlServerDbContextOptionsBuilder sql)
				{
					((RelationalDbContextOptionsBuilder<SqlServerDbContextOptionsBuilder, SqlServerOptionsExtension>)(object)sql).CommandTimeout((int?)30);
					sql.EnableRetryOnFailure(3);
					((RelationalDbContextOptionsBuilder<SqlServerDbContextOptionsBuilder, SqlServerOptionsExtension>)(object)sql).MigrationsAssembly(typeof(KargoyeriDbContext).Assembly.FullName);
				});
			}, ServiceLifetime.Scoped, ServiceLifetime.Scoped);
			services.AddScoped<ICustomerRepository, SqlCustomerRepository>();
			services.AddScoped<IShipmentRepository, SqlShipmentRepository>();
			services.AddScoped<IOperationLogRepository, SqlOperationLogRepository>();
			services.AddScoped<INotificationRepository, SqlNotificationRepository>();
			services.AddScoped<IProviderSettingsRepository, SqlProviderSettingsRepository>();
			services.AddScoped<IUnitOfWork, SqlUnitOfWork>();
		}
		else
		{
			string basePath = (Path.IsPathRooted(storageOptions.BasePath) ? storageOptions.BasePath : Path.Combine(contentRootPath, storageOptions.BasePath));
			Directory.CreateDirectory(basePath);
			services.AddSingleton((IServiceProvider _) => new JsonDatabaseStore(Path.Combine(basePath, "kargoyeri-db.json")));
			services.AddScoped<JsonDatabaseSession>();
			services.AddScoped<ICustomerRepository, JsonSessionCustomerRepository>();
			services.AddScoped<IShipmentRepository, JsonSessionShipmentRepository>();
			services.AddScoped<IOperationLogRepository, JsonSessionOperationLogRepository>();
			services.AddScoped<INotificationRepository, JsonSessionNotificationRepository>();
			services.AddScoped<IProviderSettingsRepository, JsonSessionProviderSettingsRepository>();
			services.AddScoped<IUnitOfWork, JsonUnitOfWork>();
		}
	}
}
