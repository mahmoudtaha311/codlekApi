namespace Codlek.Application.Contracts.Racks;

/// <summary>طلب كود تفعيل — الاسم إجباري والمكان لأ.</summary>
public sealed record CreateActivationCodeRequest(string? Name, string? Location);
