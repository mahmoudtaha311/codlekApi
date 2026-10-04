namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// أمر مستني قرار المحاسب.
///
/// <para>🔴 <b>ترتيب الحقول عقد.</b> فيه أربع نصوص ورا بعض
/// (<c>PublicCode · DeviceCode · DeviceName · Brand</c>) وبعدين نصّين
/// تانيين (<c>OpenedByName</c> ثم <c>FaultSummary</c>). قلب اتنين
/// متجاورين من نفس النوع بيتترجم من غير ولا تحذير.</para>
/// </summary>
public sealed record PendingRepairRow(
    Guid Id,
    string PublicCode,
    string DeviceCode,
    string DeviceName,

    /// <summary>ماركة اللاب بعد الحل — فاضية لو مش في القايمة.</summary>
    string Brand,

    /// <summary>
    /// ماركة اللاب مش في القايمة.
    ///
    /// <para>⚠️ بتتعرض للمحاسب عشان يعرف إن القيد ماتطبّقش هنا — مش
    /// عشان يمنع.</para>
    /// </summary>
    bool BrandUnknown,

    string OpenedByName,
    DateTime OpenedAtUtc,
    string FaultSummary,
    Guid? AssignedTechnicianId,
    string TechnicianName,

    /// <summary>
    /// الفني المتسند برّه ماركات اللاب.
    ///
    /// <para>🔴 المحاسب لازم يشوفها <b>قبل</b> ما يوافق — هو الوحيد
    /// اللي بيقدر يعدّي القاعدة.</para>
    /// </summary>
    bool TechnicianOutsideBrand,

    IReadOnlyList<string> Parts,

    /// <summary>
    /// راكة اشتغلت على الأمر ده وهو لسه مستني.
    ///
    /// <para>🔴 <b>أهم سطر في الصف ده لو كان صح.</b> المحاسب لسه
    /// بيقرّر، بس اللاب خلاص اتفك — ورفض ساعتها قرار مختلف تماماً عن
    /// رفض لاب لسه ماحدش لمسه.</para>
    ///
    /// <para>⚠️ وده بيحصل من راكة بنسخة قديمة مش عارفة الموافقة،
    /// بتشتغل أوفلاين. السيرفر بيقبل الشغل ويعلّمه بدل ما يرفضه، لأن
    /// الرفض بيمسح السجل الوحيد اللي بيقول إن حد فتح اللاب.</para>
    /// </summary>
    bool StartedWithoutApproval = false);
