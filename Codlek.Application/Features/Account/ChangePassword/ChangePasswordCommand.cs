using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Account;
using MediatR;

namespace Codlek.Application.Features.Account.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword) : IRequest<Result<PasswordChangedResponse>>;
