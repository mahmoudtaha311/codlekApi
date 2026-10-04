namespace Codlek.Application.Contracts.Repairs;

/// <summary>قطعة اتركّبت في الصيانة.</summary>
public sealed record RepairPartItem(
    Guid Id,
    string Name,
    string InventoryCode,
    int Quantity,
    string SerialNumber,
    string Notes);
