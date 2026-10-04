using Codlek.Application.Contracts.Reports;

namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// صف لاب في القايمة.
/// </summary>
/// <param name="Model">
/// 🔴 <b>الاسم التجاري بيغلب الكود الخام في العرض.</b>
/// «LENOVO 81FK» رقم مالوش معنى للبايع؛ «ideapad 330-15ICH» هو
/// اللي الناس بتعرفه. والخام بيفضل في <see cref="RawModel"/>
/// للتفاصيل الفنية.
/// </param>
/// <param name="StatusText">
/// ⚠️ <b>والمدموج بياخد نص مخصوص فيه كود الجهاز الكانوني</b>
/// («تم دمجه مع …») بدل «مدموج» الجافة — عشان اللي بيقرا يعرف
/// يروح فين.
/// </param>
/// <param name="MergedIntoCode">
/// 🔴 الكود الكانوني — <b>عشان الكود المتقاعد يوصّل للاب الحقيقي</b>
/// بدل ما يوقف في صفحة ميتة. الليبل المطبوع بالكود ده لسه ملزوق
/// على لاب.
/// </param>
public sealed record DeviceListItem(
    Guid Id,
    string PublicCode,
    string Manufacturer,
    string Model,
    string Status,
    string StatusText,
    string Confidence,
    string ConfidenceText,
    int TestCount,
    DateTime LastSeenAtUtc,
    LastTestSummary? Last,
    string MergedIntoCode = "",
    string RawModel = "",
    string MachineType = "",
    Guid? ContainerId = null,
    string ContainerCode = "",
    DateTime? PartChangedAtUtc = null,
    string PartChangeSummary = "",
    DeviceWhereabouts? Where = null);
