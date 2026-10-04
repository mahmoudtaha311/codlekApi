namespace Codlek.Application.Contracts.Racks;

/// <summary>طلب إلغاء محطة — <b>السبب إجباري</b>.</summary>
public sealed record RevokeStationRequest(string? Reason);
