using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Infrastructure.Data;

namespace Codlek.Infrastructure.Auth;

/// <inheritdoc cref="ILoginEventLog"/>
public sealed class LoginEventLog(AppDbContext db) : ILoginEventLog
{
    public void Add(LoginEvent entry) => db.LoginEvents.Add(entry);
}
