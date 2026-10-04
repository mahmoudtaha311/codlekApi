namespace Codlek.Tests;

/// <summary>قاعدة فحوص إنتاجية الفنيين.</summary>
public sealed class TechnicianDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_technicians_test";
}
