namespace Codlek.Application.Features.Maintenance;

/// <summary>حدود لفّات الصيانة.</summary>
public static class MaintenanceLimits
{
    /// <summary>
    /// ⚠️ أكبر دفعة مسموحة. الدفعات موجودة عشان الذاكرة على الاستضافة
    /// قليلة — دفعة بحجم الجدول كله بتلغي السبب ده.
    /// </summary>
    public const int MaxBatchSize = 1000;
}
