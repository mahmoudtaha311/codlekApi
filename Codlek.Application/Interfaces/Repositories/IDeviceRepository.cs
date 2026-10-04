using Codlek.Core.Entities;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// الأجهزة.
///
/// <para>🔴 <b>الفلتر مكتوب مرة واحدة — القايمة والتصدير بيبنيوا
/// نفس الكائن.</b> في القديم كان مكتوب مرتين بنفس النص، ومكتوب في
/// التعليق هناك إن «لو اتغيّرت في واحد لازم تتغيّر في التاني» —
/// والتلات فلاتر الأخيرة (التحذيرات والتسليم والجهة) كانوا <b>ناقصين
/// من التصدير</b> فعلاً.</para>
/// </summary>
public interface IDeviceRepository
{
    /// <summary>صفحة من القايمة + العدد الكلي قبل التصفيح.</summary>
    Task<(IReadOnlyList<DeviceListRow> Rows, int TotalItems)> ListAsync(
        Guid tenantId, DeviceListFilter filter, CancellationToken ct = default);

    /// <summary>
    /// نفس الفلتر، <b>بلا تصفيح</b> — للتصدير.
    /// </summary>
    /// <param name="cap">
    /// 🔴 <b>سقف الصفوف — والمستودع بيجيب <c>cap + 1</c>.</b>
    ///
    /// <para>الصف الزيادة هو اللي بيخلّي المنادي يعرف إن فيه قص
    /// ويقوله <b>جوّه الملف</b>. ملف مقصوص في صمت بيتقري على إنه كل
    /// البيانات — والمدير بيبني عليه قرار جرد.</para>
    /// </param>
    Task<IReadOnlyList<DeviceExportRow>> ExportAsync(
        Guid tenantId, DeviceListFilter filter, int cap, CancellationToken ct = default);

    /// <summary>
    /// لاب بكوده — <b>للبحث السريع في الترويسة</b>.
    ///
    /// <para>⚠️ بيدوّر في المدموجين كمان: الكود المتقاعد مطبوع على
    /// ليبل ملزوق على لاب حقيقي.</para>
    /// </summary>
    Task<Device?> FindByCodeAsync(
        Guid tenantId, string publicCode, CancellationToken ct = default);

    /// <summary>
    /// كل لاب الكود ده يخصّه — <b>الحالي والتاريخي</b>.
    ///
    /// <para>🔴 <b>ده اللي بيخلّي الاستيكر القديم يفضل شغّال.</b>
    /// المطابقة على <c>PublicCode</c> لوحدها معناها إن أول ما اللاب
    /// ياخد كود جديد، القديم يبقى ورقة مالهاش معنى. وكل كود عدّى على
    /// اللاب متسجّل كمرساة <c>CompanyCode</c>، فالبحث بيعدّي
    /// عليهم.</para>
    ///
    /// <para>🔴 <b>والمدموج <u>داخل</u>.</b> الكود المتقاعد مطبوع على
    /// ليبل ملزوق على لاب حقيقي — فلترة المدموجين هنا كانت بتخلّي
    /// الفني يمسح استيكر موجود في إيده ويلاقي «مش موجود».</para>
    ///
    /// <para>⚠️ <b>وفي القديم فيه تعليق بيقول العكس</b> («المدموجة
    /// مابتتعرضش — <c>Scoped</c> بيشيلهم») وهو <b>غلط</b>:
    /// <c>Scoped</c> بيقيّد بالشركة وبس، واللي بيشيل المدموجين هو
    /// <c>ScopedActive</c>. الكود كان صح والتعليق كان قديم —
    /// واللي اتنقل هنا هو السلوك مش التعليق.</para>
    ///
    /// <para>⚠️ والترتيب مقصود: <b>الحالي الأول</b>.</para>
    /// </summary>
    /// <param name="code">
    /// الكود بعد ما <c>DeviceScan.Parse</c> يقراه — <b>كبير الحروف</b>.
    /// </param>
    /// <param name="normalizedCode">
    /// 🔴 <b>موحّد بـ<c>ArabicText.Normalize</c>، مش
    /// <c>IdentityValues.Normalize</c>.</b>
    ///
    /// <para>عمود <c>NormalizedValue</c> في مرساة <c>CompanyCode</c>
    /// بيتكتب بالمطبّع <b>العربي</b> (من خدمة المزامنة)، فالمقارنة
    /// لازم تكون بنفسه. والمطبّع العربي <b>مابيكبّرش الحروف</b> —
    /// فالكود لازم يكون كبير خلاص قبل ما يوصل هنا، وده اللي
    /// <c>DeviceScan.Parse</c> بيضمنه.</para>
    /// </param>
    Task<IReadOnlyList<DeviceCodeHit>> ResolveCodeAsync(
        Guid tenantId, string code, string normalizedCode, CancellationToken ct = default);

    // =================================================================
    //  صفحة اللاب
    // =================================================================

    /// <summary>
    /// لاب واحد بمعرّفه — <b>مقيّد بالشركة</b>.
    ///
    /// <para>🔴 <b>ومقصود إنها مش <c>FindAsync(id)</c>.</b> الأخيرة
    /// بتجيب الصف بمفتاحه من غير ما تبص على الشركة، فمعرّف متخمّن أو
    /// منسوخ من شركة تانية كان هيفتح صفحتها.</para>
    ///
    /// <para>⚠️ <b>وبترجّع المدموج كمان.</b> صفحة اللاب هي صفحة
    /// تاريخه: اللاب اللي اندمج في غيره صفحته لازم تفتح وتقول إنه
    /// اندمج، مش ترجّع <c>404</c>.</para>
    /// </summary>
    Task<Device?> FindDetailAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// مراسي هوية اللاب — <b>والملغية معاها</b>.
    ///
    /// <para>🔴 <b>فيه نسختين من فلتر المراسي في المشروع:</b> واحدة
    /// بتاخد النشطة بس (للمطابقة والبحث)، ودي بتاخد <b>الكل</b>.
    /// المرساة اللي اتلغت هي اللي بتفسّر ليه بوردة اتغيّرت أو هارد
    /// اتبدّل — إخفاؤها بيخلّي الصفحة تقول حاجة ناقصة.</para>
    ///
    /// <para>⚠️ والترتيب: النشطة الأول، وبعدين بالنوع.</para>
    /// </summary>
    Task<IReadOnlyList<DeviceIdentifierRow>> IdentifiersAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// فحوص اللاب — صفحة + العدد الكلي.
    ///
    /// <para>⚠️ <b>والممسوح مستبعد.</b> الفحص الممسوح بيفضل في
    /// القاعدة كدليل، بس عدّه في تاريخ اللاب بيدّي رقم غلط.</para>
    /// </summary>
    Task<(IReadOnlyList<DeviceTestRow> Rows, int TotalItems)> TestsAsync(
        Guid tenantId, Guid deviceId, int page, int pageSize,
        CancellationToken ct = default);

    /// <summary>ملاحظات اللاب — الأحدث الأول.</summary>
    Task<IReadOnlyList<DeviceNote>> NotesAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>أسماء الجهات اللي في الصفحة — قراية واحدة.</summary>
    Task<IReadOnlyDictionary<Guid, string>> LocationNamesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);

    /// <summary>أسماء وأكواد الفنيين الحائزين — قراية واحدة.</summary>
    Task<IReadOnlyDictionary<Guid, TechnicianLabel>> HolderNamesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);

    /// <summary>أكواد الراكات — قراية واحدة.</summary>
    Task<IReadOnlyDictionary<Guid, string>> RackCodesAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);

    /// <summary>
    /// مكان كل راكة — <b>«اتفحص في أنهي دور»</b>.
    ///
    /// <para>⚠️ وده سؤال مختلف عن «هو فين دلوقتي»: اللاب ممكن يكون
    /// اتفحص في الدور التاني وهو دلوقتي في مخزن المبيعات.</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> RackLocationsAsync(
        Guid tenantId, IEnumerable<Guid?> ids, CancellationToken ct = default);
}
