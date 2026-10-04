using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// فلاتر قايمة الأجهزة — <b>متظبّطة خلاص</b>.
///
/// <para>🔴 <b>وده نفس الكائن اللي التصدير بيبنيه.</b> الزرار في
/// الواجهة مكتوب فوقه «اللي مفلتر على الشاشة مفلتر في الإكسل» —
/// وفي القديم ماكانش صح: فلاتر التحذيرات والتسليم والجهة كانت
/// بتتطبّق على الجدول بس، فالمدير يفلتر على «مخزن الجاهز» ويصدّر
/// فيطلعله <b>كل</b> الأجهزة. وملف أوسع من المطلوب في صمت أسوأ من
/// ملف بيرفض يتعمل.</para>
///
/// <para>⚠️ وده كان مكتوب <b>مرتين</b> في القديم (القايمة
/// والتصدير) بنفس النص — ومكتوب في التعليق هناك إن «لو اتغيّرت في
/// واحد لازم تتغيّر في التاني». بقى كائن واحد.</para>
/// </summary>
public sealed record DeviceListFilter
{
    /// <summary>
    /// 🔴 <b>المدموج داخل لو المستخدم بيدوّر أو طالب الحالة
    /// صراحةً.</b>
    ///
    /// <para>القايمة الافتراضية من غير المدموجين: الجهاز المدموج مش
    /// لاب مستقل، وعرضه بيخلّي العدّ يقول ١٠ لابات والحقيقة ٧.
    /// لكن <b>البحث بالكود المتقاعد لازم يفضل شغّال</b> — الكود ده
    /// مطبوع على ليبل ملزوق على لاب حقيقي.</para>
    /// </summary>
    public bool IncludeMerged { get; init; }

    /// <summary>نص البحث الخام — للمقارنة المضبوطة مع كود اللاب.</summary>
    public string? ExactCode { get; init; }

    /// <summary>
    /// قيمة مرساة الهوية الموحّدة — للبحث بالسيريال.
    ///
    /// <para>⚠️ توحيد <b>تقني</b> (تكبير حروف ولمّ مساحات)، مش
    /// التوحيد العربي.</para>
    /// </summary>
    public string? IdentityValue { get; init; }

    /// <summary>نمط <c>LIKE</c> على نص البحث — موحّد عربي ومهرّب.</summary>
    public string? SearchPattern { get; init; }

    public DeviceLifecycleStatus? Status { get; init; }

    public DeviceIdentityConfidence? Confidence { get; init; }

    /// <summary>كود فني — اللاب اللي ليه فحص من الفني ده.</summary>
    public string? TechnicianCode { get; init; }

    public Guid? RackId { get; init; }

    public Guid? ContainerId { get; init; }

    public DeviceOperationalStage? Stage { get; init; }

    /// <summary>
    /// فلتر أيقونة التحذيرات.
    ///
    /// <para>🔴 <b>الجرس بيعدّ تلات أسباب، والسيرفر كان بيفلتر
    /// واحد.</b> كان فيه «قطعة اتغيّرت» وبس، فحتى لما الرابط اتصلّح
    /// كان سببين من التلاتة مالهمش طريق — وتحذير مالوش طريق للأجهزة
    /// اللي بيتكلم عنها مجرد إزعاج.</para>
    /// </summary>
    public DeviceAttentionFlag Flag { get; init; } = DeviceAttentionFlag.Any;

    /// <summary>«خرج ولا لسه».</summary>
    public DeviceHandoverFilter Handover { get; init; } = DeviceHandoverFilter.Any;

    /// <summary>
    /// جهة بعينها — «كل جهة عندها أنهي أجهزة».
    ///
    /// <para>⚠️ ومنفصل عن <see cref="Handover"/> عن قصد: ده بيسأل
    /// «فين بالظبط»، والتاني بيسأل «خرج ولا لسه». والسؤالين
    /// بيتجمّعوا عادي.</para>
    /// </summary>
    public Guid? LocationId { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    /// <summary>
    /// نتيجة <b>آخر</b> فحص.
    ///
    /// <para>⚠️ «آخر فحص» بيتقاس على الفحص الأحدث وبس؛ وباقي
    /// الفلاتر على <b>أي</b> فحص. والفرق مقصود: «آخر فحص فيه مشكلة»
    /// حالة قايمة، و«اتفحص بالراكة دي» حقيقة تاريخية.</para>
    /// </summary>
    public DeviceOutcomeFilter Outcome { get; init; } = DeviceOutcomeFilter.Any;

    /// <summary>
    /// 🔴 <b>«الأقدم الأول» ترتيب على السيرفر مش في المتصفح.</b>
    ///
    /// <para>صاحب الشغل طلب يشوف اللاب اللي واقف من زمان. ولو
    /// الترتيب اتعمل في الواجهة، هيرتّب <b>الصفحة المعروضة</b> بس —
    /// يعني أقدم لاب في الورشة ممكن يكون في صفحة ٤ وعمره ما يطلع
    /// فوق.</para>
    /// </summary>
    public bool OldestStageFirst { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = Core.Paging.Paging.DefaultPageSize;
}
