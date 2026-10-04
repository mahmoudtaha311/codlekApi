namespace Codlek.Tests;

/// <summary>قاعدة فحوص دخول الفنيين من المحطات.</summary>
public sealed class TechnicianLoginDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_tech_login_test";
}
