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
    /// أرقام صفحة اللاب وآخر فحص وأقدم فني — <b>في قراية واحدة</b>.
    ///
    /// <para>⚠️ ودي سبع قرايات على القاعدة جوّه الدالة، بس المنادي
    /// بيشوف نداء واحد: الصفحة محتاجة الأربعة عدّادات وآخر فحص
    /// وأقدم اسم مع بعض، ولو كل واحدة بقت دالة، المعالج كان هيبقى
    /// سبع أسطر <c>await</c> ورا بعض ومحدّش يعرف إنهم سبع رحلات.</para>
    ///
    /// <para>🔴 <b>وكل عدّاد بنطاقه</b> — راجع
    /// <see cref="DeviceDetailFacts"/>.</para>
    /// </summary>
    Task<DeviceDetailFacts> DetailFactsAsync(
        Guid tenantId, Device device, CancellationToken ct = default);

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

    // =================================================================
    //  خط الزمن
    // =================================================================

    /// <summary>
    /// أعداد مصادر خط الزمن — <b>من نفس الاستعلامات اللي الصفوف
    /// بتتقرا منها</b>.
    /// </summary>
    Task<DeviceTimelineCounts> TimelineCountsAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// أعلى <paramref name="need"/> فحص — <b>زائد كل المتعادلين
    /// معاهم</b>.
    ///
    /// <para>🔴 <b>وزيادة المتعادلين مش احتياط.</b> مفتاح الترتيب
    /// بيفك التعادل بمعرّف الصف <b>كنص</b>، وSQL بيرتّب
    /// <c>uniqueidentifier</c> بترتيب بايتات <b>مختلف</b>. فلو حافة
    /// القصّة وقعت جوّه مجموعة بنفس التوقيت، اللي SQL بيقصّه مش
    /// بالضرورة اللي ترتيبنا عايزه — والنتيجة صف بيتكرر في صفحتين
    /// أو يختفي خالص.</para>
    ///
    /// <para>⚠️ والحل: نجيب المجموعة المتعادلة كاملة ونسيب ترتيبنا
    /// يقرّر. وفيه سقف على صفوف التعادل عشان مجموعة ضخمة
    /// (استيراد دفعة) ماتسحبش القاعدة.</para>
    /// </summary>
    Task<IReadOnlyList<TimelineReportRow>> TimelineReportsAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default);

    /// <summary>
    /// أعلى <paramref name="need"/> ملاحظة.
    ///
    /// <para>⚠️ ومفيش مشكلة الحافة هنا: مفتاح الملاحظة
    /// <c>bigint</c>، وترتيبه في SQL هو نفسه ترتيب الرقم مصفوفاً
    /// لتسعتاشر خانة في مفتاحنا — فقصّة SQL بتطابق ترتيبنا
    /// بالظبط.</para>
    /// </summary>
    Task<IReadOnlyList<TimelineNoteRow>> TimelineNotesAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default);

    /// <summary>
    /// أعلى <paramref name="need"/> لحظة صيانة — <b>زائد
    /// المتعادلين</b>، نفس فخ الفحوص بالحرف.
    /// </summary>
    Task<IReadOnlyList<TimelineRepairMomentRow>> TimelineRepairMomentsAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default);

    /// <summary>
    /// أعلى <paramref name="need"/> حركة.
    ///
    /// <para>🔴 <b>وحركات الصيانة التلاتة مستبعدة</b> — أمر الصيانة
    /// نفسه معروض بمعلومات أكتر، وعرضهم كمان معناه كل صيانة مكتوبة
    /// مرتين في نفس اللحظة. والقايمة في
    /// <c>DeviceMovementTitle.HiddenFromTimeline</c>.</para>
    /// </summary>
    Task<IReadOnlyList<TimelineMovementRow>> TimelineMovementsAsync(
        Guid tenantId, Guid deviceId, int need, CancellationToken ct = default);

    /// <summary>ملاحظات اللاب — الأحدث الأول.</summary>
    Task<IReadOnlyList<DeviceNote>> NotesAsync(
        Guid tenantId, Guid deviceId, CancellationToken ct = default);

    /// <summary>
    /// ملاحظة جديدة — <b>بتتضاف ومابتتحفظش</b>.
    ///
    /// <para>⚠️ الحفظ على <c>IUnitOfWork</c>، زي باقي المستودعات.</para>
    /// </summary>
    void AddNote(DeviceNote note);

    /// <summary>
    /// نسخ البرنامج لصفحة فحوص اللاب — <b>قراية واحدة للصفحة</b>.
    ///
    /// <para>🔴 <b>مقيّدة بالشركة جوّه الاستعلام نفسه</b> — مش بس
    /// بالمعرّفات اللي اتبعتت. لو المنادي غلط في تجميعها، الشرط ده
    /// لسه بيمنع قراية فحص شركة تانية.</para>
    ///
    /// <para>⚠️ والفحص اللي مالوش صف في الناتج (أو حمولته مش JSON
    /// سليم) قيمه <c>null</c> — والمعالج بيعرضها «غير متاح».</para>
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ReportVersionFacts>> TestVersionsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> reportIds, CancellationToken ct = default);

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
