using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using MediatR;

namespace Codlek.Application.Auth.Login;

/// <summary>تسجيل دخول باسم وباسورد.</summary>
public sealed record LoginCommand(string Username, string Password)
    : IRequest<Result<AuthResponse>>;
