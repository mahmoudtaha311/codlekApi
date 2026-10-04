namespace Codlek.Tests;

/// <summary>قاعدة فحوص قايمة الصيانة.</summary>
public sealed class RepairListDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_repairlist_test";
}
