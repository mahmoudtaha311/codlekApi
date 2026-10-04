namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// صفحة اللاب — <b>٣٢ خانة، والترتيب عقد</b>.
///
/// <para>⚠️ <b>والأسماء دي هي اللي على السلك.</b> اللوحة بتقراها
/// بالأسماء دي بالظبط؛ أي إعادة تسمية = خانة فاضية في الشاشة من
/// غير أي خطأ.</para>
/// </summary>
/// <param name="Model">
/// 🔴 <b>الاسم التجاري لو موجود، والخام لو مفيش.</b>
///
/// <para>«LENOVO 81FK» رقم مصنع، مش اسم. الناس بتعرف اللاب بـ
/// «ideapad 330-15ICH». والخام بيفضل في
/// <paramref name="RawModel"/> تحت.</para>
/// </param>
/// <param name="StatusText">
/// 🔴 <b>وده <u>مش</u> نص القايمة.</b> القايمة بتقول للمدموج «تم
/// دمجه مع <c>كود</c>»؛ الصفحة بتقول «مدموج» وبس. نقطتين، ونصّين
/// مختلفين لنفس القيمة عن قصد — ونسخ نسخة القايمة هنا بيغيّر خانة
/// مجمّدة.
/// </param>
/// <param name="IdentityBasisParts">
/// ⚠️ <b>نفس <paramref name="IdentityBasis"/> مفصولة.</b> الاتنين
/// في نفس الحمولة: الواجهة بتعرض الأصل كنص وبترسم الأجزاء
/// كشارات. والفاضي بيبقى قايمة فاضية — <b>مش</b> قايمة فيها نص
/// فاضي.
/// </param>
/// <param name="FirstSeenTechnicianName">
/// ⚠️ <b>من <u>أقدم</u> فحص، مش أحدث واحد.</b> الخانة دي بتقول
/// «مين اكتشف اللاب» — سؤال تاريخي. والاسم متكرر على كل فحص، فلو
/// اتصحّح يوم ما، «أحدث فحص» بيكتب على واقعة قديمة اسم ماكانش
/// موجود وقتها.
/// </param>
/// <param name="QrPayload">
/// 🔴 <b>الكود العام بالظبط ومفيش غيره.</b> مفيش رابط، ومفيش مخطّط
/// <c>codlek:</c>، ومفيش معرّف داخلي. ولاب لسه ماخدش كود بيرجّع نص
/// فاضي، والواجهة مالهاش ترسم رمز.
/// </param>
/// <param name="RawModel">
/// 🔴 <b>مابيتدهسش أبداً.</b> <paramref name="Model"/> فوق بقى
/// الاسم التجاري لما يكون موجود، بس الرقم ده مكتوب على استيكر تحت
/// اللاب والفني بيدوّر بيه في قطع الغيار — فمكانه التفاصيل الفنية،
/// مش سلة المهملات.
/// </param>
/// <param name="CommercialModelSource">
/// ⚠️ منين جه الاسم التجاري — <c>SMBIOS (Product Version)</c>
/// مثلاً. بيتعرض عشان اللي بيراجع يعرف إن الاسم اتقرا من العتاد مش
/// اتكتب بالإيد. فاضي = اللاب على الخام.
/// </param>
/// <param name="Where">
/// ⚠️ <b>النوع قابل للفراغ بس القيمة <u>عمرها ما</u> بتبقى
/// فاضية.</b> الشاشة بتحرس <c>{d.where &amp;&amp; …}</c>، فإرجاع
/// <c>null</c> بيعدّي من المصرّف وبيفضّي اللوحة في صمت — واللوحة
/// دي اتضافت أصلاً لأن الصفحة كانت أفقر من القايمة.
/// </param>
public sealed record DeviceDetail(
    Guid Id,
    string PublicCode,
    string Manufacturer,
    string Model,
    string Status,
    string StatusText,
    string Confidence,
    string ConfidenceText,
    int CodeState,
    string CodeStateText,
    string IdentityBasis,
    IReadOnlyList<string> IdentityBasisParts,
    DateTime FirstSeenAtUtc,
    string FirstSeenTechnicianCode,
    string FirstSeenTechnicianName,
    string FirstSeenRackCode,
    DateTime LastSeenAtUtc,
    string LatestTechnicianCode,
    string LatestTechnicianName,
    string LatestRackCode,
    DateTime? LatestReportAtUtc,
    string QrPayload,
    int ReportCount,
    int NoteCount,
    int IdentifierCount,
    int SnapshotCount,
    string RawModel = "",
    string MachineType = "",
    string CommercialModelSource = "",
    Guid? ContainerId = null,
    string ContainerCode = "",
    DeviceWhereabouts? Where = null);
