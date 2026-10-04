namespace Codlek.Tests;

/// <summary>قاعدة فحوص التحليلات.</summary>
public sealed class AnalyticsDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_analytics_test";
}
