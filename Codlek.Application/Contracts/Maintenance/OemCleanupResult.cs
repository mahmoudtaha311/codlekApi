namespace Codlek.Application.Contracts.Maintenance;

/// <summary>كام صف اتشال منه اسم تجاري أصله كود مصنّع.</summary>
public sealed record OemCleanupResult(int Devices, int Reports)
{
    public int Total => Devices + Reports;
}
