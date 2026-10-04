using Codlek.Application.Abstractions;
using Codlek.Application.Contracts.Common;
using Codlek.Application.Contracts.Repairs;
using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Devices;
using Codlek.Core.Enums;
using Codlek.Core.Paging;
using Codlek.Core.Repairs;
using Codlek.Core.Text;
using Codlek.Core.Time;
using MediatR;

namespace Codlek.Application.Features.Repairs.GetRepairs;

/// <summary>
/// بيبني قايمة الصيانة المصفّحة.
///
/// <para>🔴 <b>كل الترجمة والحسابات هنا — بعد ما الصفوف تيجي من
/// القاعدة.</b> أي <c>RepairStatusRules.Text(...)</c> أو
/// <c>ArabicText.Normalize(...)</c> جوّه الإسقاط بيترجم عادي
/// وبيعدّي فحوص الوحدة، وبيرمي على قاعدة حقيقية.</para>
/// </summary>
public sealed class GetRepairsQueryHandler(
    IRepairRepository repairs,
    ICurrentUser me)
    : IRequestHandler<GetRepairsQuery, Result<PagedResult<RepairListItem>>>
{
    /// <summary>⚠️ القيمة الوحيدة اللي بتقلب الترتيب. أي حاجة تانية = الأحدث.</summary>
    private const string Oldest = "oldest";

    public async Task<Result<PagedResult<RepairListItem>>> Handle(
        GetRepairsQuery query, CancellationToken cancellationToken)
    {
        var (page, size) = Paging.Clamp(query.Page, query.PageSize);

        string search = (query.Search ?? "").Trim();

        var filter = new RepairListFilter
        {
            /*
              ⚠️ **الكود بيتقارن خام، ونص البحث بيتوحّد.**

              كود الأمر لاتيني (<c>RP-00000123</c>) فالتوحيد العربي
              مالوش لازمة عليه؛ ونص البحث لازم يتوحّد عشان «أحمد»
              و«احمد» يلاقوا نفس الصف.
            */
            ExactCode = search.Length == 0 ? null : search,

            SearchPattern = search.Length == 0
                ? null
                : SearchPattern.Contains(ArabicText.Normalize(search)),

            /*
              🔴 **الفلتر المش مفهوم بيتجاهل — مابيرفضش.</b>

              <c>TryParse</c> بيفشل فالقيمة بتبقى <c>null</c>، يعني
              «مفيش فلتر». ولو رجّعنا <c>400</c>، رابط محفوظ فيه حالة
              قديمة كان بيفضّي الشاشة والمدير مش عارف ليه.
            */
            Status = Parse<RepairStatus>(query.Status),
            Approval = Parse<RepairApproval>(query.Approval),

            TechnicianId = query.Technician,

            /*
              🔴 **المدى نصف مفتوح وبيحترم التوقيت الصيفي.</b>

              <c>CairoDay</c> بتحسب بداية اليوم بتوقيت القاهرة
              الحقيقي، مش بـ<c>+2</c> ثابتة. ومصر بتقدّم الساعة
              **نص الليل**، فاليوم اللي بيتقدّم فيه مالوش نص ليل
              أصلاً — راجع <c>CairoDay</c>.
            */
            FromUtc = CairoDay.StartUtc(query.From),
            ToUtc = CairoDay.AfterUtc(query.To),

            Oldest = string.Equals(query.Sort, Oldest, StringComparison.OrdinalIgnoreCase),

            Page = page,
            PageSize = size,
        };

        var (rows, total) = await repairs.ListAsync(me.TenantId, filter, cancellationToken);

        /*
          🔴 **العدد ده استعلام تاني مستقل — برّه كل الفلاتر وبرّه
          التصفيح.</b>

          لو اتحسب من الصفوف المعروضة، «فيه ٣ مستنيين موافقة» كانت
          بتختفي لو التلاتة في صفحة ٣ أو لو الفلتر على «قيد الصيانة»
          — والمحاسب بيقفل الشاشة وهو فاكر إن مفيش حاجة واقفة عليه.
        */
        int awaiting = await repairs.CountAwaitingApprovalAsync(
            me.TenantId, cancellationToken);

        // ⚠️ وقت واحد لكل الصفوف: لفّة بتقرا `UtcNow` كل مرة بتدّي
        // أعمار مختلفة لنفس الصفحة.
        var now = DateTime.UtcNow;

        var items = rows.Select(r => Map(r, now)).ToList();

        return Result.Success(new PagedResult<RepairListItem>(
            Items: items,
            Page: page,
            PageSize: size,
            TotalItems: total,
            TotalPages: Paging.TotalPages(total, size),
            AwaitingApproval: awaiting));
    }

    /// <summary>
    /// ⚠️ <b>بالاسم مش بالرقم.</b> الداش بورد بتبعت
    /// <c>status=InProgress</c>؛ و<c>TryParse</c> بيقبل الأرقام كمان،
    /// فـ<c>status=99</c> بيعدّي كـ<c>(RepairStatus)99</c> ويرجّع
    /// قايمة فاضية بدل ما يتجاهل. ومنقول زي ما هو — القديم كان بيعمل
    /// نفس الحاجة، والقايمة الفاضية مش ضرر.
    /// </summary>
    private static T? Parse<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : null;

    private static RepairListItem Map(RepairListRow r, DateTime nowUtc) =>
        new(
            Id: r.Id,
            PublicCode: r.PublicCode,
            DeviceId: r.DeviceId,
            DeviceCode: r.DeviceCode,
            DeviceName: DeviceNaming.Display(
                r.Manufacturer, r.CommercialModelName, r.RawModel),

            // ⚠️ اسم القيمة كنص — الواجهة بتفلتر عليه، فهو عقد.
            Status: r.Status.ToString(),

            StatusText: RepairStatusRules.Text(r.Status),
            FaultSummary: r.FaultSummary,
            AssignedTechnicianId: r.AssignedTechnicianId,
            AssignedTechnicianName: r.AssignedTechnicianName,
            OpenedAtUtc: r.OpenedAtUtc,
            StartedAtUtc: r.StartedAtUtc,
            CompletedAtUtc: r.CompletedAtUtc,
            DurationMs: RepairTiming.DurationMs(r.StartedAtUtc, r.CompletedAtUtc),
            LocationName: r.LocationName,
            IssueCount: r.IssueCount,
            PartCount: r.PartCount,

            /*
              🔴 **الزوج ده بالترتيب ده.**

              الاتنين <c>double?</c> ومتجاورين، فقلبهم بيترجم من غير
              ولا تحذير — والشاشة بتعرض «قعدة الطابور» مكان «تحت إيد
              فني». أمر اتفتح من أسبوعين وبدأ من ساعة عنده
              <c>OpenAgeHours = 336</c> و<c>RepairAgeHours = 1</c>.
            */
            OpenAgeHours: RepairTiming.OpenAgeHours(r.OpenedAtUtc, r.CompletedAtUtc, nowUtc),
            RepairAgeHours: RepairTiming.RepairAgeHours(r.StartedAtUtc, r.CompletedAtUtc, nowUtc),

            Approval: (int)r.Approval,
            ApprovalText: RepairStatusRules.ApprovalText(r.Approval));
}
