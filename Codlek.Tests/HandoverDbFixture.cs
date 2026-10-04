namespace Codlek.Tests;

/// <summary>قاعدة فحوص التسليم.</summary>
public sealed class HandoverDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_handover_test";
}
