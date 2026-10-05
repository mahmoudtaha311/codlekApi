using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using MediatR;

namespace Codlek.Application.Features.Auth.Login;

/// <summary>تسجيل دخول باسم وباسورد.</summary>
/// <param name="Ip">
/// ⚠️ للسجل بس — مين حاول منين. فاضي لما النداء مش جاي من HTTP.
/// </param>
public sealed record LoginCommand(string Username, string Password, string Ip = "")
    : IRequest<Result<AuthResponse>>;
