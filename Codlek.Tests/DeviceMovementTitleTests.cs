using Codlek.Core.Devices;
using Codlek.Core.Enums;

namespace Codlek.Tests;

/// <summary>
/// عنوان الحركة على خط زمن اللاب.
///
/// <para>🔴 <b>والملف ده موجود عشان غلطة بعينها مش تتكرر:</b> فيه
/// في <c>Core</c> مساعد اسمه
/// <see cref="DeviceWorkflowEventTypeText"/> — الاسم قريب لدرجة إن
/// إعادة استعماله في خط الزمن تبان صح. وهي غلط: الكلام مختلف في
/// التلاتة المشتركين، و<b>أربع حركات ناقصة منه خالص</b> —
/// وبتتعرض بالإنجليزي الخام في صفحة عربية.</para>
/// </summary>
public class DeviceMovementTitleTests
{
    /// <summary>
    /// 🔴 <b>كل حركة <u>بتظهر</u> ليها عنوان عربي.</b>
    ///
    /// <para>والفحص بالانعكاس عن قصد: حركة جديدة بتنضاف بكرة بتوقّعه
    /// لوحدها — مش محتاجة حد يفتكر.</para>
    ///
    /// <para>⚠️ <b>ونسخة أولى من الفحص ده كانت بتقول «كل قيمة في
    /// الـenum»، ووقعت.</b> وكانت محقّة إنها توقع: تلات حركات صيانة
    /// فعلاً مالهاش عنوان — <b>عن قصد</b>، لأنها مستبعدة من خط
    /// الزمن أصلاً (أمر الصيانة معروض بمعلومات أكتر، وعرضهم كمان
    /// معناه كل صيانة مكتوبة مرتين). الفحص كان بيقيس حاجة أقوى من
    /// اللي القديم بيعملها.</para>
    /// </summary>
    [Fact]
    public void Every_shown_movement_type_has_an_arabic_title()
    {
        var missing = Enum.GetValues<DeviceWorkflowEventType>()
            .Where(DeviceMovementTitle.Shown)
            .Where(t => DeviceMovementTitle.Of(t) == t.ToString())
            .ToList();

        Assert.Empty(missing);
    }

    /// <summary>
    /// 🔴 <b>والمستبعدة مالهاش عنوان — والفحص ده بيربط الاتنين.</b>
    ///
    /// <para>لو حركة اتشالت من قايمة الاستبعاد ومااتضافش لها عنوان،
    /// بتظهر على خط الزمن بالإنجليزي الخام. والفحص ده هو اللي
    /// بيمنع الانفصال ده.</para>
    /// </summary>
    [Fact]
    public void The_hidden_movements_are_exactly_the_ones_without_a_title()
    {
        var untitled = Enum.GetValues<DeviceWorkflowEventType>()
            .Where(t => DeviceMovementTitle.Of(t) == t.ToString())
            .ToList();

        Assert.Equal(DeviceMovementTitle.HiddenFromTimeline.Order(), untitled.Order());
    }

    /// <summary>
    /// ⚠️ <b>والمستبعدة هي حركات الصيانة التلاتة بالظبط</b> — مش
    /// أكتر ومش أقل. القايمة دي بتتقرا في فلتر الاستعلام كمان.
    /// </summary>
    [Fact]
    public void Only_the_three_repair_movements_are_hidden()
    {
        Assert.Equal(
            [
                DeviceWorkflowEventType.SentToRepair,
                DeviceWorkflowEventType.RepairStarted,
                DeviceWorkflowEventType.RepairCompleted,
            ],
            DeviceMovementTitle.HiddenFromTimeline);

        Assert.False(DeviceMovementTitle.Shown(DeviceWorkflowEventType.SentToRepair));
        Assert.True(DeviceMovementTitle.Shown(DeviceWorkflowEventType.LocationMoved));
    }

    /// <summary>
    /// ⚠️ حاجز على الفحص اللي فوق: لو الانعكاس رجّع قايمة فاضية،
    /// «مفيش حركة مالهاش عنوان» بتبقى صح والفحص بيعدّي وهو مش بيقيس
    /// حاجة.
    /// </summary>
    [Fact]
    public void There_are_at_least_ten_movement_types()
    {
        Assert.True(Enum.GetValues<DeviceWorkflowEventType>().Length >= 10);
    }

    /// <summary>
    /// 🔴 <b>الأربعة اللي كانوا ناقصين من مساعد الصيانة.</b>
    ///
    /// <para>دول أعضاء ٦—٩ في الـenum والراكة بتكتبهم النهاردة. لو
    /// خط الزمن استعمل <see cref="DeviceWorkflowEventTypeText"/>،
    /// الأربعة دول كانوا بيطلعوا بأسمائهم الإنجليزية.</para>
    /// </summary>
    [Theory]
    [InlineData(DeviceWorkflowEventType.DispatchedToSales, "اتسلّم لقسم المبيعات")]
    [InlineData(DeviceWorkflowEventType.DispatchedToPointOfSale, "اتبعت لنقطة بيع")]
    [InlineData(DeviceWorkflowEventType.ReturnReceived, "رجع مرتجع")]
    [InlineData(DeviceWorkflowEventType.CodeChanged, "كود الجهاز اتغيّر")]
    public void The_four_types_the_repair_strip_omits_have_titles(
        DeviceWorkflowEventType type, string expected)
    {
        Assert.Equal(expected, DeviceMovementTitle.Of(type));

        // 🔴 ومساعد الصيانة فعلاً مالوش عنوان ليهم — ده سبب وجود
        //    الملف ده، ولو اتصلّح هناك الفحص ده بيفضل صح.
        Assert.Equal(type.ToString(), DeviceWorkflowEventTypeText.Of(type));
    }

    /// <summary>
    /// 🔴 <b>والتلاتة المشتركين كلامهم <u>مختلف</u> عن مساعد
    /// الصيانة.</b>
    ///
    /// <para>شريط الصيانة بيقول عناوين جدول قصيرة؛ وخط الزمن بيقول
    /// جمل كاملة. والفحص ده بيثبّت إن الاتنين مش بيتساووا — يعني
    /// حد دمجهم بيوقّعه.</para>
    /// </summary>
    [Theory]
    [InlineData(DeviceWorkflowEventType.StageChanged)]
    [InlineData(DeviceWorkflowEventType.CustodyHandoff)]
    [InlineData(DeviceWorkflowEventType.LocationMoved)]
    public void The_timeline_wording_differs_from_the_repair_strip(
        DeviceWorkflowEventType type)
    {
        Assert.NotEqual(DeviceWorkflowEventTypeText.Of(type), DeviceMovementTitle.Of(type));
    }

    /// <summary>
    /// ⚠️ <b>والمجهول بيظهر باسمه الخام.</b> حركة من نسخة أحدث لازم
    /// تفضل مرئية مش تختفي ولا يتقال عنها «حركة» على العمياني.
    /// </summary>
    [Fact]
    public void An_unknown_movement_type_shows_its_raw_name()
    {
        Assert.Equal("99", DeviceMovementTitle.Of((DeviceWorkflowEventType)99));
    }

    /// <summary>
    /// ⚠️ واسم المكان المش معروف «مفيش» مش خانة فاضية — السطر اللي
    /// بيقول «اتنقل لـ» وبعدها فراغ بيبان مكسور.
    /// </summary>
    [Fact]
    public void The_unknown_placeholder_is_not_an_empty_string()
    {
        Assert.NotEqual("", DeviceMovementTitle.Unknown);
        Assert.Equal("مفيش", DeviceMovementTitle.Unknown);
    }
}
