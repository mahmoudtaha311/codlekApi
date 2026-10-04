namespace Codlek.Tests;

/// <summary>قاعدة فحوص الحساب والمستودعات.</summary>
public sealed class AccountDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_accounts_test";
}
