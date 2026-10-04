namespace Codlek.Tests;

/// <summary>قاعدة فحوص مستودع الحاويات.</summary>
public sealed class ContainerDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_containers_test";
}
