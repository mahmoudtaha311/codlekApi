namespace Codlek.Application.Contracts.Racks;

/// <summary>
/// محطة فحص في قايمة الإدارة.
///
/// <para>🔴 <b>ولا <c>ApiKeyHash</c> ولا <c>Salt</c> ولا
/// <c>KeyPrefix</c> هنا.</b> المفتاح بيتعرض <b>مرة واحدة</b> وقت
/// التسجيل وبعدها بصمته بس اللي في القاعدة — فأي حقل منهم في العقد
/// ده معناه إن قايمة بسيطة بقت باب على مفاتيح المحطات كلها.</para>
///
/// <para>⚠️ <b>و<see cref="Status"/> بالإنجليزي عن قصد.</b> اللوحة
/// بتلوّن الصف منه (الملغية أحمر والموقوفة أصفر)، فهو
/// <b>معرّف</b> مش نص للقراية — والعربي في
/// <see cref="StatusText"/> جنبه.</para>
/// </summary>
public sealed record StationListItem(
    Guid Id,
    string RackCode,
    string Name,
    string Location,
    string Status,
    string StatusText,
    int ReportsReceived,
    DateTime? RegisteredAtUtc,
    DateTime? LastSeenAtUtc,
    string AppVersion);
