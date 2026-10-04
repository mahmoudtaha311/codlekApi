namespace Codlek.Application.Contracts.Technicians;

/// <summary>
/// حساب فني — <b>مفتاحه معرّف الصف، مش الكود</b>.
///
/// <para>🔴 <b>ومفيش بصمة ولا ملح ولا باسورد في العقد ده
/// خالص.</b> الباسورد الأولي بيترجّع <b>مرة واحدة</b> من الإنشاء
/// ومن إعادة التعيين عشان المدير يسلّمه للفني — وبعدها مش موجود في
/// أي مكان غير دماغ الفني.</para>
/// </summary>
/// <param name="CredentialVersion">
/// 🔴 <b>الرقم اللي بيلغي النسخ المحفوظة على الراكات.</b> النسخة
/// المحفوظة شايلة الرقم القديم؛ وأول ما الراكة تتصل وتلاقي رقم
/// أحدث بتمسح نسختها. ومن غيره، تغيير الباسورد على اللوحة مكانش
/// هيمنع الدخول بالقديم على راكة أوفلاين.
/// </param>
/// <param name="CapabilityChangedAtUtc">
/// 🔴 <b>وقت التغيير هو دليل الشغل الأوفلاين.</b> الراكة بتشتغل من
/// غير نت، فممكن فني يعمل صيانة وهو مصرّح له وبعدين الصلاحية تتسحب
/// قبل ما الشغل يترفع. والوقت ده هو اللي بيخلّي السيرفر يفرّق بين
/// «كان مصرّح له وقتها» و«مكانش مصرّح له خالص» — ومن غيره، سحب
/// صلاحية بيمسح شغل حصل فعلاً.
/// </param>
public sealed record TechnicianAccount(
    Guid Id,
    string Code,
    string DisplayName,
    string Username,
    bool IsActive,
    string SuspendedReason,
    string SuspendedByName,
    DateTime? SuspendedAtUtc,
    bool MustChangePassword,
    int CredentialVersion,
    int Specialty,
    string SpecialtyText,
    Guid? DepartmentId,
    DateTime CreatedAtUtc,
    DateTime? LastSuccessfulLoginUtc,
    bool CanTest = true,
    bool CanRepair = false,
    DateTime? CapabilityChangedAtUtc = null,

    /// <summary>
    /// 🔴 <b>القايمة الفاضية مش <c>null</c>.</b> الفني اللي مالوش
    /// ربط ماركات لازم يرجع <c>[]</c> — عشان الواجهة تفرّق بين
    /// «محمّلة ومفيش قيد» و«المسار ده مابيحمّلش الربط».
    /// </summary>
    IReadOnlyList<Guid>? BrandIds = null);
