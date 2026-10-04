namespace Codlek.Tests;

/// <summary>قاعدة فحوص التغذيات النازلة.</summary>
public sealed class RackFeedDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_rack_feeds_test";
}
