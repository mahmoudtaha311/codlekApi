using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Auth;
using MediatR;

namespace Codlek.Application.Auth.Refresh;

/// <summary>تجديد الجلسة بتوكن تجديد.</summary>
public sealed record RefreshCommand(string RefreshToken) : IRequest<Result<AuthResponse>>;
