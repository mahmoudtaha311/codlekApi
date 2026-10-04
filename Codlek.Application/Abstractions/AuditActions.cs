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
    /*
      🔴 **دي WebUser مش Technician.**

      الفنيين ليهم أكواد تانية (`technician.*`) وجدول تاني خالص.
      والخلط بيخلّي السجل يقول «اتعمل حساب فني» والحقيقة إن
      اللي اتعمل حساب لوحة بصلاحيات مدير.
    */
    public const string WebUserCreated = "webuser.created";
    public const string WebUserSuspended = "webuser.suspended";
    public const string WebUserActivated = "webuser.activated";
    public const string WebUserPasswordReset = "webuser.password_reset";

    public const string BrandCreated = "brand.created";
    public const string BrandUpdated = "brand.updated";
    public const string BrandAliasAdded = "brand.alias_added";
    public const string BrandAliasRemoved = "brand.alias_removed";

    public const string DepartmentCreated = "department.created";
    public const string DepartmentUpdated = "department.updated";
}
