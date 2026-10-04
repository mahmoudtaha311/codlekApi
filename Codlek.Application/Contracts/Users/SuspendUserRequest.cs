namespace Codlek.Application.Contracts.Users;

/// <summary>إيقاف حساب — السبب إجباري (٣ حروف على الأقل).</summary>
public sealed record SuspendUserRequest(string? Reason);
