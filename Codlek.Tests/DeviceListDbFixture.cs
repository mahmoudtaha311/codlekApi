namespace Codlek.Tests;

/// <summary>قاعدة فحوص قايمة الأجهزة.</summary>
public sealed class DeviceListDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_devicelist_test";
}
