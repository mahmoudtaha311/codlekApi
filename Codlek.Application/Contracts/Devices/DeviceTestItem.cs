using Codlek.Application.Contracts.Analytics;

namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// فحص واحد في تاريخ اللاب.
/// </summary>
/// <param name="TechnicianName">
/// ⚠️ <b>لقطة وقت الفحص، مش الاسم الحالي للفني.</b> ومعاه
/// <paramref name="TechnicianId"/> عشان الواجهة تشاور على الحساب
/// نفسه من غير ما تعتمد على الكود كمفتاح — المدير لما يصحّح اسم
/// فني، الفحوص القديمة مالهاش تتغيّر.
/// </param>
/// <param name="GeneralNote">
/// 🔴 <b>ملاحظة الفني اللي اتكتبت في آخر تاب في الفحص — وهي اللي
/// بتملّي تاب «الملاحظات» في صفحة اللاب.</b>
///
/// <para>صاحب الشغل قال الملاحظات «مش بتوصل ولا بتسمع ع الموقع» —
/// وكان محقّ من ناحيتين: الخانة كانت مشالة من الديسكتوب، والقيمة
/// كانت متخزّنة على الفحص من غير ما أي عقد يطلّعها.</para>
///
/// <para>⚠️ وفاضية في الغالبية العظمى من الفحوص القديمة — التاب
/// لازم يفلتر الفاضي بدل ما يعرض صفوف فاضية.</para>
/// </param>
/// <param name="ApplicationVersion">
/// نسخة برنامج الراكة اللي عملت الفحص — <b>من الحمولة الخام، مش
/// عمود</b>. والناقصة «غير متاح»، بنفس كلمة صفحة الفحص.
///
/// <para>⚠️ <b>ده اللي بيجاوب «ليه الفحصين دول مختلفين»</b>: نفس
/// اللاب اتفحص بنسختين وقايمة المراحل اتغيّرت بينهم.</para>
/// </param>
/// <param name="TestDefinitionVersion">نسخة تعريف الفحوص — نفس القاعدة.</param>
/// <param name="NotRunCount">
/// 🔴 <b>بيتعدّ من المراحل وقت العرض (<c>Status == 0</c>)، زي تاب
/// القديم بالظبط — مش العمود المخزّن على الفحص.</b>
///
/// <para>العمود <c>Report.NotRunCount</c> اتضاف بقيمة افتراضية صفر
/// ومااتملاش للفحوص اللي قبل ٢١-٩-٢٠٢٦، فلو قريناه كانت الشارة
/// «N مااتنفذش» هتختفي من كل فحص قديم.</para>
///
/// <para>⚠️ عشان كده الرقم ده ممكن يختلف عن نفس الرقم في صفحة الفحص
/// وقايمة الفحوص (اللي بيقروا العمود): العدّ بيحسب كمان مراحل مالهاش
/// نتيجة زي التسليم. توحيدهم مستني قرار صاحب الشغل.</para>
/// </param>
public sealed record DeviceTestItem(
    Guid ReportId,
    DateTime StartedAtUtc,
    DateTime? EndedAtUtc,
    long DurationMs,
    Guid? TechnicianId,
    string TechnicianName,
    string TechnicianCode,
    string RackCode,
    TestCounts Counts,
    int StepCount,
    string ApplicationVersion,
    string TestDefinitionVersion,
    int NotRunCount,
    string GeneralNote = "");
