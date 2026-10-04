namespace Codlek.Tests;

/// <summary>قاعدة فحوص لقطات العتاد.</summary>
public sealed class HardwareDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_hardware_test";
}
