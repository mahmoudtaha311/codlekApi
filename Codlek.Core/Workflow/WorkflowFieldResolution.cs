using Codlek.Core.Enums;

namespace Codlek.Core.Workflow;

/// <summary>
/// حلّ حقول حدث السجل التشغيلي — <b>دالة نقية</b>.
///
/// <para>🔴 <b>تلات احتمالات لكل حقل: فضّيه · حطّه · سيبه.</b> ودول
/// متكوّدين في حقلين (<c>ClearsHolder</c> و<c>ToX</c>)، وكل فرع ورا
/// عطل حصل:</para>
/// <list type="bullet">
///   <item>دمج <c>ClearsHolder</c> في «<c>ToTechnicianId == null</c>»
///         بيخلّي نقلة من رف لرف تشيل الحائز عن لاب في إيد حد.</item>
///   <item>شيل الـ<c>?? current</c> بيخلّي حركة حيازة تكتب
///         <c>ToStage = Unknown (0)</c> في التاريخ، فالخط الزمني يقول
///         «روّح لمكان مجهول».</item>
///   <item>حساب <c>StageChangedAtUtc</c> من «دلوقتي» بدل
///         <c>OccurredAtUtc</c> بيخلّي حركة راكة أوفلاين عمرها أسبوع
///         تبان النهاردة — وكل مقاييس العمر بتتبوّظ.</item>
///   <item>شيل حارس <c>stage != current</c> بيخلّي حركة بنفس المرحلة
///         تصفّر الساعة.</item>
/// </list>
/// </summary>
public static class WorkflowFieldResolution
{
    /// <summary>المرحلة اللي تتكتب في الحدث — <c>null</c> يعني «زي ما هي».</summary>
    public static DeviceOperationalStage ToStage(
        DeviceOperationalStage? requested, DeviceOperationalStage current) =>
        requested ?? current;

    /// <summary>
    /// الحائز اللي يتكتب في الحدث.
    ///
    /// <para>⚠️ <c>clearsHolder</c> بتسبق كل حاجة — «محدش ماسكه
    /// دلوقتي» قرار، مش غياب قرار.</para>
    /// </summary>
    public static Guid? ToTechnician(
        bool clearsHolder, Guid? requested, Guid? current) =>
        clearsHolder ? null : (requested ?? current);

    public static Guid? ToLocation(Guid? requested, Guid? current) =>
        requested ?? current;

    /// <summary>المرحلة على الجهاز بتتغيّر؟ — ومعاها ساعة التغيير.</summary>
    public static bool StageChanges(
        DeviceOperationalStage? requested, DeviceOperationalStage current) =>
        requested is { } stage && stage != current;

    /// <summary>
    /// الحائز على الجهاز بيتغيّر؟
    ///
    /// <para><c>null</c> يعني «ماتلمسش العمود».</para>
    /// </summary>
    public static Guid? HolderWrite(
        bool clearsHolder, Guid? requested, out bool touch)
    {
        if (clearsHolder)
        {
            touch = true;
            return null;
        }

        touch = requested is not null;
        return requested;
    }
}
