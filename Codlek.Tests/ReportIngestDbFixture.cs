namespace Codlek.Tests;

/// <summary>قاعدة فحوص استقبال الفحوص.</summary>
public sealed class ReportIngestDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_ingest_test";
}
