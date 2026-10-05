namespace Codlek.Application.Interfaces.Repositories;

/// <summary>الاسم التجاري اللي على الجهاز دلوقتي — لملء الإقلاع.</summary>
public sealed record DeviceModelRow(
    Guid Id,
    string PublicCode,
    string? CommercialModelName,
    string? CommercialModelSource,
    string? MachineType);
