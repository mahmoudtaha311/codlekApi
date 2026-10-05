namespace Codlek.Application.Features.Reports;

/// <summary>
/// نسخة البرنامج للعرض — <b>مكان واحد لصفحة الفحص وتاب فحوص اللاب</b>.
///
/// <para>⚠️ الفاضي والناقص والبايظ كلهم «غير متاح» — زي
/// <c>ReportRawFields.Text</c> في القديم. مفيش فرق عملي بينهم للي
/// بيقرا، ولو الصفحتين اختلفوا في الكلمة، نفس الفحص يبان بنسختين.</para>
/// </summary>
public static class ReportVersionText
{
    public const string Unavailable = "غير متاح";

    public static string Display(string? value) =>
        string.IsNullOrWhiteSpace(value) ? Unavailable : value;
}
