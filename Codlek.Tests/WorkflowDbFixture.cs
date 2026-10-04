namespace Codlek.Tests;

/// <summary>قاعدة فحوص مسجّل حركة الجهاز.</summary>
public sealed class WorkflowDbFixture : SqlServerDbFixture
{
    protected override string DatabaseName => "codlek_workflow_test";
}
