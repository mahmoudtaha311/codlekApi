namespace Codlek.Application.Contracts.Auth;

/// <summary>ناتج فك توكن التجديد.</summary>
public sealed record RefreshClaims(Guid UserId, int CredentialVersion);
