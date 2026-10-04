namespace Codlek.Tests;

/// <summary>قاعدة فحوص حسابات اللوحة.</summary>
public sealed class UserDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_users_test";
}
