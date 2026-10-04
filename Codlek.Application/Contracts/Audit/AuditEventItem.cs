namespace Codlek.Application.Contracts.Audit;

/// <summary>
/// سطر في سجل المراجعة.
///
/// <para>⚠️ <b>الكود والنص الاتنين في الرد.</b> النص للعرض، والكود
/// عشان الواجهة تفلتر عليه — ولو رجّعنا النص بس، الفلتر كان هيبقى
/// على نص عربي بيتغيّر مع أي تعديل لغة.</para>
/// </summary>
public sealed record AuditEventItem(
    long Id,
    DateTime OccurredAtUtc,
    string ActorType,
    string ActorTypeText,
    string ActorName,
    string Action,
    string ActionText,
    string EntityType,
    string EntityTypeText,
    string EntityCode,
    Guid? EntityId,
    string Summary,
    string Ip);
