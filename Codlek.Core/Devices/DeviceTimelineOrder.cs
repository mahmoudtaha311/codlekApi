namespace Codlek.Core.Devices;

/// <summary>
/// ترتيب خط زمن اللاب وتصفيحه — <b>دوال نقية</b>.
///
/// <para>خط الزمن بيدمج <b>خمس مصادر</b>: صف اللاب نفسه (حدث
/// الاكتشاف)، والفحوص، والملاحظات، ولحظات أوامر الصيانة، والحركات
/// التشغيلية. وكل مصدر بيرجّع أعلى <c>الصفحة × ٢٥</c> صف بترتيبه
/// هو، والدمج والترتيب بيحصلوا في الذاكرة على العدد ده وبس.</para>
///
/// <para>⚠️ <b>والحجّة إن ده كفاية:</b> أعلى <c>need</c> حدث في
/// الدمج مستحيل يحتوي أكتر من <c>need</c> صف من أي مصدر، فاللي
/// برّه أعلى <c>need</c> بتاع مصدره برّه النتيجة أكيد.</para>
/// </summary>
public static class DeviceTimelineOrder
{
    public const int PageSize = 25;

    /// <summary>
    /// أبعد صفحة بنخدمها.
    ///
    /// <para>الطريقة بتجيب «رقم الصفحة × ٢٥» صف من <b>كل</b> مصدر،
    /// فالصفحة ٢٠٠ معناها ٥٠٠٠ صف من الفحوص و٥٠٠٠ من الملاحظات. ده
    /// الحد المعلن: لاب فيه أكتر من ٥٠٠٠ حدث مش هينفع يتصفّح لآخره
    /// من هنا، وساعتها الحل استعلام بنافذة مش تكبير الرقم ده.</para>
    /// </summary>
    public const int MaxPage = 200;

    /// <summary>
    /// أولوية النوع عند تساوي التوقيت.
    ///
    /// <para>الترتيب ده سببه <b>السببية</b> مش الذوق. بالترتيب
    /// الزمني الصاعد: اللاب يتكتشف، يتفحص، الفحص يفتح أمر صيانة،
    /// الأمر يبدأ ويقفل، اللاب يتنقل، وفي الآخر حد يكتب ملاحظة عن
    /// اللي حصل. والعرض بالنازل بيقلبهم.</para>
    ///
    /// <para>🔴 <b>والقيم لازم تفضل رقم واحد (٠—٩).</b>
    /// <see cref="SortKey"/> بيلزق الرقم ده في نص بعرض <b>مش</b>
    /// ثابت، والمقارنة بعد كده نصية بترتيب البايت. يعني أولوية «١٠»
    /// بتقع <b>قبل</b> أولوية «٩» لأن <c>'1' &lt; '9'</c> — ترتيب
    /// مقلوب بيظهر في التعادلات وبس، يعني عيب بيعيش شهور قبل ما حد
    /// يشوفه. ودي نفس العائلة بتاعة العيب اللي <c>D19</c> على
    /// التيكات موجود عشانه.</para>
    ///
    /// <para>⚠️ وعشان كده كل الحركات التشغيلية بتاخد أولوية
    /// <b>واحدة</b> بدل واحدة لكل نوع. المدى فيه تسع خانات وخلاص،
    /// والحل لو خلصوا هو تصفير المفتاح لعرض ثابت — مش تزويد رقم على
    /// العمياني.</para>
    /// </summary>
    public static int Rank(DeviceTimelineEventType type) => type switch
    {
        DeviceTimelineEventType.DeviceDiscovered => 0,
        DeviceTimelineEventType.TestPerformed => 1,
        DeviceTimelineEventType.RepairOpened => 2,
        DeviceTimelineEventType.RepairStarted => 3,
        DeviceTimelineEventType.RepairCompleted => 4,
        DeviceTimelineEventType.RepairUnableToRepair => 5,
        DeviceTimelineEventType.RepairCancelled => 6,
        DeviceTimelineEventType.DeviceMoved => 7,
        DeviceTimelineEventType.NoteAdded => 8,
        _ => 9,
    };

    /// <summary>
    /// مفتاح الترتيب الكامل — <b>ترتيب كلّي، مفيش تعادل مفتوح</b>.
    ///
    /// <para>الشكل: «تيكات بتسعتاشر خانة | أولوية النوع | مفتاح
    /// الصف». التيكات بعرض <b>ثابت</b> عشان المقارنة النصية تساوي
    /// المقارنة الزمنية، وأولوية النوع بتفك التعادل بين مصدرين في
    /// نفس اللحظة، ومفتاح الصف بيفك التعادل جوّه المصدر الواحد.</para>
    ///
    /// <para>🔴 <b>ومن غير المستوى التالت، صفّين بنفس التوقيت ونفس
    /// النوع ممكن يطلعوا في صفحتين — أو يختفي واحد فيهم خالص.</b>
    /// وده بالظبط اللي كسر تصفيح الفحوص قبل كده.</para>
    /// </summary>
    public static string SortKey(DateTime atUtc, DeviceTimelineEventType type, string rowKey) =>
        atUtc.Ticks.ToString("D19") + "|" + Rank(type) + "|" + rowKey;

    /// <summary>
    /// عدد الصفحات من الإجمالي — <b>واحدة على الأقل دايماً</b>.
    ///
    /// <para>⚠️ اللاب اللي لسه ماتفحصش عنده حدث واحد (الاكتشاف)،
    /// فصفر صفحات كانت بتخلّي الواجهة تقول «صفحة ٠ من ٠».</para>
    /// </summary>
    public static int Pages(int total) =>
        Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));

    /// <summary>
    /// الصفحة اللي هتتخدم فعلاً — <b>مقصوصة على الموجود وعلى
    /// السقف</b>.
    ///
    /// <para>🔴 <b>الطلب بيترد لآخر صفحة بدل ما يرجّع فاضي.</b> رابط
    /// محفوظ على صفحة ٧ لجهاز بقى عنده صفحتين لازم يعرض حاجة — مش
    /// شاشة فاضية.</para>
    ///
    /// <para>⚠️ والسقف تاني عشان الطلب مايسحبش آلاف الصفوف: الطريقة
    /// بتجيب «الصفحة × ٢٥» من كل مصدر.</para>
    /// </summary>
    /// <param name="totalItems">
    /// ⚠️ <b>عدد <u>الأحداث</u> مش عدد الصفحات.</b> الدالة بتحسب
    /// الصفحات بنفسها من <see cref="Pages"/> — وتمرير عدد صفحات هنا
    /// بيقصّ على الرقم الغلط ويخلّي آخر الصفحات مش موصولة.
    /// </param>
    public static int ClampPage(int? requested, int totalItems) =>
        Math.Min(Math.Min(Math.Max(1, requested ?? 1), Pages(totalItems)), MaxPage);

    /// <summary>كام صف لازم من كل مصدر عشان الصفحة دي تتبني.</summary>
    public static int Need(int page) => page * PageSize;

    /// <summary>كام صف يتقفز من الدمج المرتّب.</summary>
    public static int Skip(int page) => (page - 1) * PageSize;
}
