namespace Codlek.Tests;

/// <summary>قاعدة فحوص صفحة اللاب.</summary>
public sealed class DeviceDetailDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_device_detail_test";
}
