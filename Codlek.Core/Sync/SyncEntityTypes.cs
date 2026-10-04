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
