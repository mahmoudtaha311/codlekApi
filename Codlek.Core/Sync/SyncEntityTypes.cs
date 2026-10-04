namespace Codlek.Core.Sync;

/// <summary>
/// أنواع الكيانات على سلك المزامنة — <b>أسماء مجمّدة</b>.
///
/// <para>🔴 <b>والأسماء دي نصوص على السلك.</b> الراكة بتبعتها في كل
/// عنصر في الدفعة، وبتسأل عن القايمة دي <b>قبل</b> ما تعمل أي
/// حمولة جديدة — فعملية مالهاش دعم بتتمنع من الشاشة بدل ما تتحوّل
/// لصف عالق في طابور.</para>
///
/// <para>⚠️ <b>والاسم مش اسم الكلاس.</b> <c>workitem</c> و
/// <c>workflow</c> أقصر من أسماء الكيانات عن قصد — دي أسماء
/// بروتوكول، وتغييرها لـ<c>repairworkitem</c> بيخلّي كل راكة في
/// الميدان تبعت نوع السيرفر مابيعرفهوش.</para>
/// </summary>
public static class SyncEntityTypes
{
    public const string Device = "device";
    public const string Report = "report";
    public const string RepairWorkItem = "workitem";
    public const string DeviceWorkflowEvent = "workflow";

    /// <summary>
    /// كل اسم الكود ده بيعرف يقرأه — <b>حتى لو النسخة دي مش
    /// مفعّلاه</b>.
    ///
    /// <para>🔴 <b>والفرق بينه وبين <see cref="Supported"/> بيوصل
    /// للفني.</b> اسم مش هنا خالص = حمولة غلط
    /// (<c>UnknownEntityType</c>). اسم هنا ومش مدعوم = <b>السيرفر
    /// ده</b> أقدم من العملية (<c>UnsupportedEntity</c>) — والراكة
    /// بتحفظ الصف «مستنّي تحديث الخادم» بدل «بياناتك غلط».</para>
    /// </summary>
    public static readonly IReadOnlySet<string> Known =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Device, Report, RepairWorkItem, DeviceWorkflowEvent,
        };

    /// <summary>
    /// اللي النسخة دي بتقبله فعلاً.
    ///
    /// <para>⚠️ المقارنة بتتجاهل حالة الأحرف — الراكات القديمة
    /// بتبعت <c>Device</c> بحرف كبير.</para>
    /// </summary>
    public static readonly IReadOnlySet<string> Supported =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Device, Report, RepairWorkItem, DeviceWorkflowEvent,
        };

    /// <summary>
    /// ترتيب التطبيق جوّه الدفعة الواحدة.
    ///
    /// <para>🔴 <b>الجهاز الأول دايماً</b> — كل حاجة تانية بتشاور
    /// عليه. بعده الفحص، بعده أمر الصيانة (ممكن يشاور على فحص مصدر)،
    /// وبعده الحركات (ممكن تشاور على أمر).</para>
    ///
    /// <para>⚠️ <b>والترتيب ده حزام تاني مش شرط صحّة.</b> الراكة
    /// بترتّب طابورها كمان، والحركة اللي وصلت قبل أمرها بتتقبل
    /// والرابط بيتحل بعدين.</para>
    /// </summary>
    public static int ApplyOrder(string? entityType) =>
        (entityType ?? "").ToLowerInvariant() switch
        {
            Device => 0,
            Report => 1,
            RepairWorkItem => 2,
            DeviceWorkflowEvent => 3,
            _ => 99,
        };

    /// <summary>
    /// نسخة بروتوكول المزامنة.
    ///
    /// <para>١ = الأجهزة والفحوص بس. ٢ = ومعاها الشغل التشغيلي.</para>
    /// </summary>
    public const int ProtocolVersion = 2;

    /// <summary>
    /// نسخة شكل الحمولة لكل نوع.
    ///
    /// <para>بتزيد لما شكل الحمولة يتغيّر بطريقة السيرفر القديم
    /// مايفهمهاش. والراكة بتقارن وبتمتنع لو نسختها أحدث.</para>
    /// </summary>
    public static readonly IReadOnlyDictionary<string, int> PayloadVersions =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Device] = 1,
            [Report] = 1,
            [RepairWorkItem] = 1,
            [DeviceWorkflowEvent] = 1,
        };
}
