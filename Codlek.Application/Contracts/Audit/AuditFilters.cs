namespace Codlek.Application.Contracts.Audit;

/// <summary>
/// خيارات فلترة السجل — <b>بتتبني من الصفوف الموجودة فعلاً</b>.
///
/// <para>⚠️ <b>مش من قايمة ثابتة بكل الأكواد.</b> القايمة الثابتة
/// كانت هتدّي فلاتر بتطلّع صفر دايماً، والمستخدم بيفتكر إن السجل
/// ناقص.</para>
/// </summary>
public sealed record AuditFilters(
    IReadOnlyList<NamedOption> Actions,
    IReadOnlyList<NamedOption> EntityTypes,
    IReadOnlyList<string> Actors);
