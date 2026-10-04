namespace Codlek.Tests;

/// <summary>قاعدة فحوص الجلسات.</summary>
public sealed class LoginSessionDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_sessions_test";
}
