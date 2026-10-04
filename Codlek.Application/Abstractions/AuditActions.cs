namespace Codlek.Application.Abstractions;

/// <summary>
/// أكواد أحداث المراجعة.
///
/// <para>🔴 <b>الأكواد دي عقد مخزّن.</b> متكتوبة في صفوف موجودة فعلاً
/// في جدول <c>AuditEvents</c> بالإنتاج. تغيير نص أي كود بيفصل الصفوف
/// القديمة عن اسمها. <b>الإضافة مسموحة، وإعادة التسمية لأ.</b></para>
///
/// <para>⚠️ والقايمة دي بتتزاد مع كل قطاع بينتقل. اللي موجود هنا هو
/// اللي القطاعات المنقولة بتستعمله.</para>
/// </summary>
public static class AuditActions
{
    public const string DepartmentCreated = "department.created";
    public const string DepartmentUpdated = "department.updated";
}
