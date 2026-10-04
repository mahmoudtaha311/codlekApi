namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// وصف الفترة في كل رد تحليلات.
///
/// <para>⚠️ <b>موجود في كل رد عن قصد.</b> الواجهة بتعرض «آخر ٣٠
/// يوم» فوق الأرقام؛ ومن غيره، الرقم بيبان من غير سياقه وأي رابط
/// محفوظ بيبان كأنه بيعرض النهاردة.</para>
/// </summary>
public sealed record PeriodInfo(
    string Key,
    string Label,
    string FromDate,
    string ToDate,
    int Days,
    bool Hourly);
