namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// حِمل راكة في الفترة.
///
/// <para>⚠️ <b>الراكات كلها بترجع — والراكة اللي ماشتغلتش في الفترة
/// بترجع بصفر.</b> إخفاؤها بيخلّي «مفيش شغل عليها» و«مش موجودة»
/// شكلهم واحد.</para>
/// </summary>
public sealed record RackActivityItem(
    Guid Id,
    string Code,
    string Name,
    string Status,
    string StatusText,
    int Reports,
    DateTime? LastSeenAtUtc);
