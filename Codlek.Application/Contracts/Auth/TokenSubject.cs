namespace Codlek.Application.Contracts.Auth;

/// <summary>ملخّص المستخدم اللي بيتحط في التوكن.</summary>
public sealed record TokenSubject(
    Guid UserId,
    Guid TenantId,
    string Username,
    string DisplayName,
    string Code,
    string Role,
    int CredentialVersion,
    bool MustChangePassword);
