using Codlek.Application.Abstractions;
using Codlek.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Codlek.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler(
    ILoginSessions sessions,
    ILogger<LogoutCommandHandler> log)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        int closed = await sessions.EndAllAsync(command.UserId, "خروج", cancellationToken);

        log.LogInformation("خروج — {UserId}، اتقفلت {Count} جلسة.",
            command.UserId, closed);

        // ⚠️ صفر جلسات **نجاح** مش فشل. المستخدم عايز يبقى خارج، وهو
        // خارج فعلاً. الرد بـ«مفيش جلسات» بيخلّي الواجهة تعرض خطأ على
        // عملية نجحت.
        return Result.Success();
    }
}
