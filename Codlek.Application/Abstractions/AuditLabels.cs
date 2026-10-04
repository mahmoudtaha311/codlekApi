namespace Codlek.Application.Abstractions;

/// <summary>
/// ترجمة أكواد سجل المراجعة للعربي.
///
/// <para>🔴 <b>مفيش اعتماد على الذاكرة هنا — فيه فحص بالانعكاس
/// بيعدّي على كل ثابت في <see cref="AuditActions"/> ويقع لو واحد
/// مالوش ترجمة.</b></para>
///
/// <para>⚠️ والفحص ده اتكتب في المشروع القديم بعد ما
/// <c>device.identity_merged</c> فضل <b>شهور</b> بيتعرض بالإنجليزي
/// الخام في سجل عربي، ومكانش باين لأنه بيتكتب من خدمة مالهاش نقطة
/// نهاية.</para>
///
/// <para>⚠️ <b>والكود المجهول بيرجع بنفسه، مش بـ«غير معروف».</b>
/// سطر سجل مالوش ترجمة لازم يفضل <b>مقروء</b> — إخفاؤه بيخلّي
/// الرقابة ناقصة من غير ما حد يعرف.</para>
/// </summary>
public static class AuditLabels
{
    private static readonly Dictionary<string, string> Actions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [AuditActions.RepairCreated] = "فتح أمر صيانة",
            [AuditActions.RepairAssigned] = "إسناد أمر صيانة",
            [AuditActions.RepairReassigned] = "إعادة إسناد أمر صيانة",
            [AuditActions.RepairApproved] = "موافقة على أمر صيانة",
            [AuditActions.RepairRejected] = "رفض أمر صيانة",
            [AuditActions.RepairStarted] = "بدء صيانة",
            [AuditActions.RepairCompleted] = "إنهاء صيانة",
            [AuditActions.RepairUnableToRepair] = "تعذّر إصلاح",
            [AuditActions.RepairCancelled] = "إلغاء أمر صيانة",
            [AuditActions.RepairRetestStarted] = "بدء إعادة فحص بعد صيانة",
            [AuditActions.DeviceSentToRepair] = "إرسال جهاز للصيانة",
            [AuditActions.DevicesHandedOver] = "تسليم لابات لجهة",
            [AuditActions.DevicesMarkedReady] = "مراجعة لابات وتعليمها جاهزة",
            [AuditActions.WebUserCreated] = "إنشاء حساب لوحة تحكم",
            [AuditActions.WebUserSuspended] = "إيقاف حساب لوحة تحكم",
            [AuditActions.WebUserActivated] = "إعادة تفعيل حساب لوحة تحكم",
            [AuditActions.WebUserPasswordReset] = "إعادة تعيين كلمة مرور حساب لوحة تحكم",
            [AuditActions.BrandCreated] = "إنشاء ماركة",
            [AuditActions.BrandUpdated] = "تعديل ماركة",
            [AuditActions.BrandAliasAdded] = "إضافة اسم بديل لماركة",
            [AuditActions.BrandAliasRemoved] = "شيل اسم بديل من ماركة",
            [AuditActions.DepartmentCreated] = "إنشاء قسم",
            [AuditActions.DepartmentUpdated] = "تعديل قسم",
        };

    public static string Action(string? action)
    {
        if (string.IsNullOrWhiteSpace(action)) return "—";

        return Actions.TryGetValue(action, out string? label) ? label : action;
    }

    /// <summary>
    /// ⚠️ <b>نوع الكيان بيتقارن بحروف صغيرة.</b> سجل الإنتاج فيه
    /// <c>"repair"</c> و<c>"Department"</c> و<c>"Device"</c> —
    /// الاختلاف موجود في البيانات، فالترجمة لازم تعدّي عليه.
    /// </summary>
    public static string Entity(string? entityType) =>
        (entityType ?? "").ToLowerInvariant() switch
        {
            "rack" => "محطة فحص",
            "technician" => "فني",
            "report" => "فحص",
            "device" => "جهاز",
            "user" => "مستخدم",
            "repair" => "أمر صيانة",
            "" => "—",
            _ => entityType!,
        };

    public static string Actor(string? actorType) =>
        (actorType ?? "").ToLowerInvariant() switch
        {
            "user" => "مستخدم",
            "rack" => "محطة فحص",
            "system" => "النظام",
            _ => "—",
        };

    /// <summary>
    /// كل أكواد الإجراءات المعرّفة — <b>بالانعكاس</b>.
    ///
    /// <para>⚠️ الانعكاس هنا مقصود: هو اللي بيخلّي فحص «كل كود ليه
    /// ترجمة» يلاقي الكود الجديد تلقائياً. القايمة المكتوبة بالإيد
    /// كانت هتتنسى.</para>
    /// </summary>
    public static IReadOnlyList<string> DefinedActions() =>
        typeof(AuditActions)
            .GetFields(System.Reflection.BindingFlags.Public
                     | System.Reflection.BindingFlags.Static)
            .Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
}
