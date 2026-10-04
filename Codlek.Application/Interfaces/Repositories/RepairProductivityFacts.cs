namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// إنتاجية صيانة فني واحد.
///
/// <para>⚠️ <b>والمدة بتتحسب بالثواني في SQL وبتتقسم بعد
/// الجمع.</b> فرق الدقايق في SQL Server بيعدّ <b>عبور حدود
/// الدقيقة</b> مش الوقت اللي فات: من ١٠:٠٠:٥٩ لـ١٠:٠١:٠٠ بيقول
/// دقيقة كاملة. على صيانة بتتقاس بالساعات الفرق ضايع، بس على
/// تصليحة عشر دقايق بيبقى خطأ عشرة في المية.</para>
/// </summary>
public sealed record RepairProductivityFacts(
    string Name,
    int Count,
    double AverageMinutes,
    DateTime? LastAtUtc);
