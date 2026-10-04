namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// تفصيل اللوحة — <b>نقطة تانية عن قصد</b>.
///
/// <para>⚠️ اللوحة الرئيسية بتتحمّل أول ما الصفحة تفتح؛ والتفصيل ده
/// خمس تجميعات زيادة، فلو اتحطّوا في نفس الرد كل فتحة صفحة كانت
/// بتدفع تمنهم.</para>
/// </summary>
public sealed record DashboardBreakdownResponse(
    PeriodInfo Period,
    IReadOnlyList<TechnicianActivityItem> Technicians,
    IReadOnlyList<RackActivityItem> Racks,
    IReadOnlyList<NamedCountItem> Failures,
    IReadOnlyList<DurationBucketItem> Durations,
    DeviceMix Devices);
