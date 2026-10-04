namespace Codlek.Tests;

/// <summary>قاعدة فحوص العدّاد الذرّي.</summary>
public sealed class CounterDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_counters_test";
}
