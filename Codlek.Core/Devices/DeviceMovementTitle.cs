using Codlek.Core.Enums;

namespace Codlek.Core.Devices;

/// <summary>
/// عنوان الحركة التشغيلية على <b>خط زمن اللاب</b>.
///
/// <para>🔴 <b>وده <u>مش</u> <see cref="DeviceWorkflowEventTypeText"/>
/// — والاتنين لازم يفضلوا منفصلين.</b></para>
///
/// <para>الاسمين قريبين من بعض لدرجة إن إعادة الاستعمال تبان صح،
/// وهي غلط من حاجتين:</para>
///
/// <list type="number">
///   <item><b>الكلام مختلف في التلاتة المشتركين.</b> شريط الصيانة
///   بيقول «تغيير مرحلة» و«تسليم» و«نقل مكان» — عناوين جدول قصيرة.
///   وخط الزمن بيقول «اتغيّرت مرحلة الجهاز» و«اتسلّم الجهاز لحد
///   تاني» و«اتنقل الجهاز لمكان تاني» — جمل كاملة في سطر حكاية.
///   دمجهم بيكسر واحد من الاتنين.</item>
///
///   <item>🔴 <b>وأربع حركات ناقصة من بتاع الصيانة خالص:</b>
///   <c>DispatchedToSales</c> و<c>DispatchedToPointOfSale</c>
///   و<c>ReturnReceived</c> و<c>CodeChanged</c>. دي أعضاء ٦—٩ في
///   <see cref="DeviceWorkflowEventType"/>، والراكة بتكتبهم
///   <b>النهاردة</b>. يعني إعادة الاستعمال معناها أربع حركات
///   حقيقية بتتعرض بالإنجليزي الخام جوّه خط زمن عربي.</item>
/// </list>
///
/// <para>⚠️ <b>والمجهول بيظهر باسمه الخام</b> — نوع حركة من نسخة
/// أحدث لازم يفضل <b>مرئي</b>، مش يختفي ولا يتقال عنه «حركة» على
/// العمياني. واللي بيراجع تاريخ لاب محتاج يعرف إن فيه حركة مش
/// متعرّفة، مش إن مفيش.</para>
/// </summary>
public static class DeviceMovementTitle
{
    /// <summary>
    /// 🔴 <b>الحركات اللي <u>مابتظهرش</u> على خط الزمن — ومعاها
    /// السبب.</b>
    ///
    /// <para>التلاتة دول بيتكتبوا مع <b>كل</b> تغيير حالة صيانة،
    /// وأمر الصيانة نفسه معروض أصلاً وبمعلومات أكتر (الرقم، الفني،
    /// اللي اتعمل). عرضهم كمان معناه كل صيانة مكتوبة <b>مرتين في
    /// نفس اللحظة</b>.</para>
    ///
    /// <para>🔴 <b>والقايمة دي في <c>Core</c> عشان الاستعلام
    /// والعنوان ميختلفوش.</b> الفلتر في المستودع بيقراها من هنا،
    /// وعشان كده مالهمش عنوان عربي هنا — وده مقصود مش سهو. ولو
    /// واحد اتشال من القايمة ومااتضافش له عنوان، بيظهر بالإنجليزي
    /// — وفيه فحص بيربط الاتنين.</para>
    ///
    /// <para>⚠️ والاستبعاد <b>بالنوع</b> مش بمعرّف أمر الصيانة: نقل
    /// مكان أو تسليم حيازة ممكن يبقى مربوط بأمر صيانة وهو برضه
    /// حركة حقيقية لازم تبان.</para>
    /// </summary>
    public static readonly DeviceWorkflowEventType[] HiddenFromTimeline =
    [
        DeviceWorkflowEventType.SentToRepair,
        DeviceWorkflowEventType.RepairStarted,
        DeviceWorkflowEventType.RepairCompleted,
    ];

    /// <summary>الحركة دي بتظهر على خط الزمن؟</summary>
    public static bool Shown(DeviceWorkflowEventType type) =>
        !HiddenFromTimeline.Contains(type);

    public static string Of(DeviceWorkflowEventType type) => type switch
    {
        DeviceWorkflowEventType.StageChanged => "اتغيّرت مرحلة الجهاز",
        DeviceWorkflowEventType.CustodyHandoff => "اتسلّم الجهاز لحد تاني",
        DeviceWorkflowEventType.LocationMoved => "اتنقل الجهاز لمكان تاني",
        DeviceWorkflowEventType.DispatchedToSales => "اتسلّم لقسم المبيعات",
        DeviceWorkflowEventType.DispatchedToPointOfSale => "اتبعت لنقطة بيع",
        DeviceWorkflowEventType.ReturnReceived => "رجع مرتجع",
        DeviceWorkflowEventType.CodeChanged => "كود الجهاز اتغيّر",
        _ => type.ToString(),
    };

    /// <summary>
    /// ⚠️ <b>اسم المكان المش معروف = «مفيش»، مش خانة فاضية.</b>
    /// سطر خط زمن بيقول «اتنقل لـ» وبعدها فراغ بيبان مكسور؛ و«مفيش»
    /// بتقول الحقيقة: الصف مالوش مكان متسجّل.
    /// </summary>
    public const string Unknown = "مفيش";
}
