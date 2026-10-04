using Codlek.Application.Contracts.Analytics;

namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// حدث واحد على خط زمن اللاب.
/// </summary>
/// <param name="Kind">
/// 🔴 <b>اسم الخانة <c>Kind</c> مش <c>Type</c> — ودي خانة
/// مجمّدة.</b> اللوحة بتختار الأيقونة من <c>kind</c>، فإعادة
/// تسميتها بتكسر كل سطر على الخط الزمني.
///
/// <para>⚠️ والقيمة هي <b>اسم</b> نوع الحدث كنص
/// (<c>TestPerformed</c>) — معرّف للواجهة مش نص للقراية.</para>
/// </param>
/// <param name="ReportId">
/// ⚠️ <b>بترجع <c>null</c> دايماً — خانة ميتة متنقولة زي ما هي.</b>
///
/// <para>القديم بيبعت <c>null</c> في المكان ده لكل حدث، حتى أحداث
/// الفحص اللي ليها معرّف فحص فعلاً. واللوحة مابتقراهاش. وملّيها
/// تغيير سلوك مش إصلاح، فاتنقلت كما هي.</para>
/// </param>
/// <param name="Counts">
/// ⚠️ عدّادات الفحص — <c>null</c> لأي حدث تاني.
/// </param>
/// <param name="NoteBody">
/// ⚠️ نص الملاحظة — <c>null</c> لأي حدث تاني. ومنفصل عن
/// <paramref name="Summary"/> عشان الواجهة تعرضه كاقتباس.
/// </param>
public sealed record TimelineEventItem(
    string Kind,
    DateTime AtUtc,
    string Title,
    string Summary,
    string ActorName,
    string ActorCode,
    Guid? ReportId,
    TestCounts? Counts,
    string? NoteBody);
