using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// محطات الفحص وأكواد تفعيلها.
///
/// <para>🔴 <b>الصفوف بتترجّع <u>متتبّعة</u> في نقط الكتابة:</b>
/// الإيقاف والإلغاء بيعدّلوا الصف في مكانه، والحفظ من وحدة العمل —
/// عشان السجل والتغيير ينزلوا في حفظة واحدة.</para>
///
/// <para>⚠️ <b>ومفيش دالة واحدة هنا بتاخد معرّف شركة من
/// الطلب.</b> الشركة بتيجي من التوكن دايماً — ولو أخدها من الطلب،
/// مالك شركة كان هيقدر يلغي محطة في شركة تانية.</para>
/// </summary>
public interface IRackRepository
{
    /// <summary>كل محطات الشركة، مرتّبة بالكود.</summary>
    Task<IReadOnlyList<Rack>> ListAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// محطة بعينها — <b>متتبّعة</b>.
    ///
    /// <para>⚠️ <c>null</c> هنا معناها «مش في الشركة دي» — وممكن
    /// تكون موجودة في شركة تانية. والرسالة مابتفرّقش عن قصد.</para>
    /// </summary>
    Task<Rack?> FindAsync(
        Guid tenantId, Guid rackId, CancellationToken ct = default);

    /// <summary>
    /// المحطات المرشّحة لمفتاح ببادئة معيّنة — <b>النشطة وبس</b>.
    ///
    /// <para>🔴 <b>والبادئة بتضيّق البحث من غير ما تكشف
    /// المفتاح.</b> التحقق التشفيري غالي، فتشغيله على كل راكة في
    /// القاعدة مع كل طلب كان بيخلّي كل مزامنة تكلّف وقت معالج
    /// حقيقي.</para>
    ///
    /// <para>🔴 <b>وشرط <c>Active</c> جوّه الاستعلام مش بعده.</b>
    /// المحطة الموقوفة أو الملغية بتقع على أول خطوة قبل ما أي اسم
    /// أو شركة يتقرا — ومفيش <c>403</c>، <c>401</c> فاضية. اللي
    /// بيحاول مالوش يعرف إيه اللي ناقص.</para>
    ///
    /// <para>⚠️ <b>ومفيش ترشيح بالشركة هنا — ولا ممكن يكون
    /// فيه.</b> المفتاح هو اللي بيحدّد الشركة، فالترشيح بيها قبل
    /// التحقق دور.</para>
    ///
    /// <para>⚠️ <b>و<c>Rack.IsActive</c> محسوبة مش عمود</b>
    /// (<c>[NotMapped]</c>) — استعمالها في <c>Where</c> مابيترجمش
    /// وبيخلّي كل نداء راكة يرجّع <c>500</c>.</para>
    /// </summary>
    Task<IReadOnlyList<Rack>> ActiveByKeyPrefixAsync(
        string keyPrefix, CancellationToken ct = default);

    // =================================================================
    //  أكواد التفعيل
    // =================================================================

    /// <summary>
    /// الأكواد اللي <b>لسه مااتستعملتش</b>، الأجدد الأول.
    ///
    /// <para>🔴 <b>والمستهلك مش في القايمة.</b> الكود اللي اتفعّلت
    /// بيه محطة خلاص بقى سجل مراجعة مش كود مستني — وعرضه كان بيخلّي
    /// المدير يفتكر إن عنده أكواد جاهزة أكتر من الحقيقة.</para>
    /// </summary>
    Task<IReadOnlyList<RackPairingCode>> PendingCodesAsync(
        Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// كود بعينه — <b>متتبّع</b> عشان المسح.
    ///
    /// <para>⚠️ <b>بيرجّع المستهلك كمان.</b> المسح محتاج يشوفه عشان
    /// يرفضه برسالة صح — لو الاستعلام فلتره، الرد كان هيبقى «مش
    /// موجود» وده غلط.</para>
    /// </summary>
    Task<RackPairingCode?> FindCodeAsync(
        Guid tenantId, Guid codeId, CancellationToken ct = default);

    /*
      ⚠️ **ومفيش فحص تكرار للبادئة — عن قصد.**

      التسجيل بيدوّر بالبادئة على كل الأكواد المستنية (في كل
      الشركات) وبيتحقق من البصمة الكاملة لكل واحد، ولو مالقاش
      بيزوّد عدّاد المحاولات الغلط **لكلهم**. يعني كودين بنفس
      البادئة بيتأثروا ببعض: حد غلط في واحد بيخصم من التاني.

      🔴 **والزيادة الجماعية دي مقصودة في القديم** — هي اللي
      بتخلّي التخمين غير مجدي بدل ما يبقى مجرد إبطاء، فمش حاجة
      تتصلّح.

      والتكرار نفسه: ٣١ حرف أس ٤ = ٩٢٣٬٥٢١ بادئة. بخمس أكواد
      مستنية، احتمال التصادم أقل من ٠٠٠٥٪ — حلقة وقراية قاعدة
      لحاجة بتحصل مرة كل مئتين ألف مش مكسب.
    */

    /// <summary>
    /// أكواد التفعيل المرشّحة لبادئة — <b>المستنية بس</b>.
    ///
    /// <para>🔴 <b>ومفيش ترشيح بالشركة هنا — ولا ممكن يكون
    /// فيه.</b> المحطة الجديدة مالهاش مفتاح ومالهاش شركة؛ الكود هو
    /// اللي بيحدّد الشركة. فالبحث عابر للشركات <b>بالضرورة</b>.</para>
    ///
    /// <para>⚠️ <b>ومفيش ترشيح بالانتهاء كمان.</b> الكود المنتهي
    /// لازم يترشّح عشان الرد عليه يبقى «انتهت صلاحيته» مش «غلط» —
    /// والفرق ده بيوفّر على الفني مكالمة. وكمان محاولته الغلط
    /// بتتعدّ زي الباقي.</para>
    ///
    /// <para>⚠️ <b>متتبّعة</b> — عدّاد المحاولات الغلط بيزيد في
    /// مكانه.</para>
    /// </summary>
    Task<IReadOnlyList<RackPairingCode>> CodesByPrefixAsync(
        string prefix, CancellationToken ct = default);

    /// <summary>
    /// بيستهلك الكود — <b>تحديث مشروط بعدّ الصفوف</b>.
    ///
    /// <para>🔴 <b>مش قراية وبعدها كتابة.</b> راكتين بيسجّلوا بنفس
    /// الكود في نفس اللحظة لازم <b>واحدة بس</b> تكسب، والقراية-ثم-
    /// الكتابة بتخلّي الاتنين يكسبوا — يعني محطتين بمفتاحين من كود
    /// واحد.</para>
    ///
    /// <para>⚠️ وبيرجّع <c>false</c> لو حد سبقنا.</para>
    /// </summary>
    Task<bool> ConsumeCodeAsync(
        Guid codeId, DateTime atUtc, CancellationToken ct = default);

    /// <summary>بيربط الكود المستهلك بالمحطة اللي اتعملت منه.</summary>
    Task LinkCodeToRackAsync(
        Guid codeId, Guid rackId, CancellationToken ct = default);

    void Add(Rack rack);

    /// <summary>
    /// محطات تانية بنفس هوية القرص — <b>مؤشّر استنساخ</b>.
    ///
    /// <para>⚠️ <b>والملغية مستبعدة:</b> محطة اتلغت وهوية قرصها
    /// اتسجّلت تاني ده تسجيل جديد مشروع مش استنساخ.</para>
    /// </summary>
    Task<IReadOnlyList<Rack>> TwinsByInstallationAsync(
        Guid tenantId, string installationId, Guid exceptRackId,
        CancellationToken ct = default);

    /// <summary>اسم الشركة — للرد على التسجيل.</summary>
    Task<string?> TenantNameAsync(Guid tenantId, CancellationToken ct = default);

    void AddCode(RackPairingCode code);

    void RemoveCode(RackPairingCode code);
}
