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
    // =================================================================
    //  الصيانة
    // =================================================================
    //
    // 🔴 **الـentityType معاهم هو النص الصغير `"repair"`** — مش
    // `"Repair"`. ونقطة فلاتر السجل بتبني المنسدلة من
    // القيم الموجودة فعلاً، فأي فرق في حالة الحروف بيطلّع
    // سطرين لنفس الحاجة.
    //
    // ⚠️ وقطاع الأقسام بيكتب `"Department"` بحرف كبير —
    // المشروعين عندهم اصطلاحين مختلفين، والسجل المشترك
    // بيخلّي الفرق باين.

    public const string RepairCreated = "repair.created";
    public const string RepairAssigned = "repair.assigned";
    public const string RepairReassigned = "repair.reassigned";
    public const string RepairApproved = "repair.approved";
    public const string RepairRejected = "repair.rejected";
    public const string RepairStarted = "repair.started";
    public const string RepairCompleted = "repair.completed";
    public const string RepairUnableToRepair = "repair.unable_to_repair";
    public const string RepairCancelled = "repair.cancelled";
    public const string RepairRetestStarted = "repair.retest_started";
    public const string DeviceSentToRepair = "device.sent_to_repair";

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

    // =================================================================
    //  التسليم
    // =================================================================

    /// <summary>
    /// ⚠️ <c>"Device"</c> بحرف كبير هو نوع الكيان هنا — زي
    /// <c>"Department"</c> ومش زي <c>"repair"</c>. الفرق موجود في
    /// بيانات الإنتاج واتنقل زي ما هو.
    /// </summary>
    public const string DevicesMarkedReady = "device.marked_ready";

    public const string DevicesHandedOver = "device.handed_over";

    // =================================================================
    //  حسابات الفنيين
    // =================================================================

    /// <summary>
    /// ⚠️ نوع الكيان هنا <c>"Technician"</c> بحرف كبير — زي
    /// <c>"Device"</c> و<c>"Department"</c>، ومش زي <c>"repair"</c>.
    /// الفرق موجود في بيانات الإنتاج واتنقل زي ما هو.
    /// </summary>
    public const string TechnicianCreated = "technician.created";

    public const string TechnicianSuspended = "technician.suspended";

    public const string TechnicianActivated = "technician.activated";

    public const string TechnicianPasswordReset = "technician.password_reset";

    public const string TechnicianUpdated = "technician.updated";

    /// <summary>
    /// 🔴 سطر منفصل عن «اتعدّلت البيانات» — تعديل اسم مالوش يسيب
    /// سطر «اتغيّرت الصلاحيات».
    /// </summary>
    public const string TechnicianCapabilityChanged = "technician.capability_changed";

    public const string TechnicianBrandsChanged = "technician.brands_changed";
}
