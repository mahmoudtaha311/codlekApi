using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using Codlek.Application.Interfaces;
using MediatR;

namespace Codlek.Application.Auth.Refresh;

/// <summary>
/// بيجدّد الجلسة.
///
/// <para>⚠️ القرار كله جوّه <see cref="ILoginSessions.RefreshAsync"/> —
/// الحتة دي بتترجم الناتج لرد الواجهة وبس.</para>
/// </summary>
public sealed class RefreshCommandHandler(ILoginSessions sessions)
    : IRequestHandler<RefreshCommand, Result<AuthResponse>>
{
    public async Task<Result<AuthResponse>> Handle(
        RefreshCommand command, CancellationToken cancellationToken)
    {
        var result = await sessions.RefreshAsync(command.RefreshToken, cancellationToken);

        if (result.IsFailure) return Result.Failure<AuthResponse>(result.Error);

        var (tokens, subject) = result.Value!;

        return Result.Success(new AuthResponse(
            tokens.AccessToken,
            tokens.RefreshToken,
            tokens.ExpiresInSeconds,
            subject.UserId,
            subject.DisplayName,
            subject.Role,
            subject.MustChangePassword));
    }
}
