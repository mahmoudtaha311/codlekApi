namespace Codlek.Tests;

/// <summary>قاعدة فحوص قايمة الفحوص وتفاصيلها.</summary>
public sealed class ReportDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_reports_test";
}
