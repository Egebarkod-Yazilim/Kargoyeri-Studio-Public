using System;
using System.Linq;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Application.Services;

internal static class ShipmentMapping
{
	public static CargoShipment BuildTransientShipment(CreateShipmentRequest request)
	{
		OrderSourceChannel? orderSourceChannel = (request.SourceChannel.HasValue ? new OrderSourceChannel?((OrderSourceChannel)request.SourceChannel.Value) : null);
		string sourceChannelCode = ((!string.IsNullOrWhiteSpace(request.SourceChannelCode)) ? request.SourceChannelCode.Trim().ToLowerInvariant() : OrderSourceChannelCodes.GetCode(orderSourceChannel));
		return CargoShipment.Create(GenerateShipmentReference(request.TenantKey, orderSourceChannel), request.TenantKey, request.OrderReference, request.ClientShipmentReference, request.IdempotencyKey, MapProvider(request.Provider), MapSource(request.Source), request.CollectionAmount, request.CurrencyCode, MapAddress(request.Sender), MapAddress(request.Recipient), request.Packages.Select(MapPackage), request.Metadata, orderSourceChannel, sourceChannelCode);
	}

	public static string GenerateShipmentReference(string tenantKey, OrderSourceChannel? channel = null)
	{
		string text = new string(tenantKey.Where(char.IsLetterOrDigit).ToArray());
		if (string.IsNullOrWhiteSpace(text))
		{
			text = "tenant";
		}
		string prefix = OrderSourceChannelCodes.GetPrefix(channel);
		string text2 = $"KY-{text.ToUpperInvariant()}-{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
		return (text2.Length > 48) ? text2.Substring(0, 48) : text2;
	}

	public static CargoProviderType MapProvider(CargoProviderTypeDto provider)
	{
		return (CargoProviderType)provider;
	}

	public static CargoProviderTypeDto MapProvider(CargoProviderType provider)
	{
		return (CargoProviderTypeDto)provider;
	}

	public static IntegrationSourceType MapSource(IntegrationSourceTypeDto source)
	{
		return (IntegrationSourceType)source;
	}

	public static IntegrationSourceTypeDto MapSource(IntegrationSourceType source)
	{
		return (IntegrationSourceTypeDto)source;
	}

	public static ShipmentStatusDto MapStatus(ShipmentStatus status)
	{
		return (ShipmentStatusDto)status;
	}

	public static AddressInfo MapAddress(AddressDto dto)
	{
		return new AddressInfo
		{
			Name = dto.Name,
			CompanyName = dto.CompanyName,
			Phone = dto.Phone,
			Email = dto.Email,
			CountryCode = dto.CountryCode,
			City = dto.City,
			District = dto.District,
			PostalCode = dto.PostalCode,
			AddressLine1 = dto.AddressLine1,
			AddressLine2 = dto.AddressLine2
		};
	}

	public static AddressDto MapAddress(AddressInfo info)
	{
		return new AddressDto
		{
			Name = info.Name,
			CompanyName = info.CompanyName,
			Phone = info.Phone,
			Email = info.Email,
			CountryCode = info.CountryCode,
			City = info.City,
			District = info.District,
			PostalCode = info.PostalCode,
			AddressLine1 = info.AddressLine1,
			AddressLine2 = info.AddressLine2
		};
	}

	public static PackageInfo MapPackage(PackageDto dto)
	{
		return new PackageInfo
		{
			PackageSequence = dto.PackageSequence,
			Weight = dto.Weight,
			Desi = dto.Desi,
			Width = dto.Width,
			Height = dto.Height,
			Length = dto.Length,
			Description = dto.Description,
			CashOnDeliveryAmount = dto.CashOnDeliveryAmount
		};
	}

	public static PackageDto MapPackage(PackageInfo info)
	{
		return new PackageDto
		{
			PackageSequence = info.PackageSequence,
			Weight = info.Weight,
			Desi = info.Desi,
			Width = info.Width,
			Height = info.Height,
			Length = info.Length,
			Description = info.Description,
			CashOnDeliveryAmount = info.CashOnDeliveryAmount
		};
	}
}
