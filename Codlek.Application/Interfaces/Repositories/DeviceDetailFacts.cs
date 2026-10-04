namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// أرقام صفحة اللاب — <b>كل عدّاد بنطاقه</b>.
///
/// <para>🔴 <b>والنطاقات <u>مختلفة</u>، وده أهم حاجة في الصف ده:</b></para>
///
/// <list type="bullet">
///   <item><see cref="ReportCount"/> — الفحوص <b>غير الممسوحة</b>.
///   الفحص الممسوح بيفضل في القاعدة كدليل، وعدّه بيدّي تاريخ لاب
///   غلط.</item>
///
///   <item><see cref="IdentifierCount"/> — <b>كل</b> المراسي
///   والملغية معاها. دي نفس القايمة اللي النقطة بترجّعها، فلو
///   العدّاد عدّ النشطة بس كان الرقم بيخالف عدد الصفوف المعروضة.</item>
///
///   <item><see cref="SnapshotCount"/> — الفحوص اللي معاها لقطة
///   عتاد، مش عدد القطع.</item>
/// </list>
///
/// <para>⚠️ وأي فحص بيقارن الأربعة لازم يبقى <b>على قاعدة
/// حقيقية</b>: المستودع المزيّف بينفّذ الفلاتر بنفسه فمابيفرّقش
/// بين نطاق ونطاق.</para>
/// </summary>
/// <param name="LatestTechnicianCode">من <b>أحدث</b> فحص — «آخر مين لمسه».</param>
/// <param name="FirstSeenTechnicianName">
/// من <b>أقدم</b> فحص فيه اسم — الدليل المعاصر لواقعة الاكتشاف.
/// </param>
public sealed record DeviceDetailFacts(
    int ReportCount,
    int NoteCount,
    int IdentifierCount,
    int SnapshotCount,
    DateTime? LatestReportAtUtc,
    string LatestTechnicianCode,
    string LatestTechnicianName,
    Guid? LatestRackId,
    string FirstSeenTechnicianName,
    string ContainerCode);
