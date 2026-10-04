namespace Codlek.Tests;

/// <summary>قاعدة فحوص محطات الفحص.</summary>
public sealed class RackDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_racks_test";
}
