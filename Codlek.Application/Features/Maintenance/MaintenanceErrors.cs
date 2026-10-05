using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Maintenance;

/// <summary>
/// أسباب فشل صيانة الإقلاع.
///
/// <para>⚠️ مفيش حد بيشوفها غير سجل السيرفر — مفيش نقطة للوحة بتنده
/// الأوامر دي.</para>
/// </summary>
public static class MaintenanceErrors
{
    public static Error OwnerSeedFailed(string reason) =>
        new("maintenance.owner_seed_failed", $"فشل عمل حساب المدير الافتراضي: {reason}", 500);
}
