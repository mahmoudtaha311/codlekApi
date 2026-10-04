using Codlek.Application.Interfaces;
using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Paging;
using Codlek.Core.Text;
using Codlek.Core.Time;

namespace Codlek.Application.Features.Reports;

/// <summary>
/// بناء فلتر الفحوص — <b>مكان واحد للقايمة وللتصدير</b>.
/// </summary>
internal static class ReportFilters
{
    public static ReportListFilter Build(
        ICurrentUser me,
        string? search,
        string? result,
        string? technician,
        Guid? container,
        DateTime? from,
        DateTime? to,
        int? page = null,
        int? pageSize = null)
    {
        var (p, size) = Paging.Clamp(page, pageSize);

        string raw = (search ?? "").Trim();

        return new ReportListFilter
        {
            /*
              🔴 **الفني بيشوف شغله هو، والمدير بيفلتر.**

              الترتيب ده مهم: لو الفلتر غلب، الفني كان بيقدر يشوف
              شغل حد تاني بتغيير رابط.
            */
            TechnicianCode = me.IsManagerOrAbove
                ? (string.IsNullOrWhiteSpace(technician) ? null : technician.Trim())
                : me.Code,

            ExactDeviceCode = raw.Length == 0 ? null : raw,

            SearchPattern = raw.Length == 0
                ? null
                : SearchPattern.Contains(ArabicText.Normalize(raw)),

            Result = (result ?? "").Trim().ToLowerInvariant() switch
            {
                "healthy" => ReportResultFilter.Healthy,
                "repair" => ReportResultFilter.NeedsRepair,
                "nohard" => ReportResultFilter.NoHard,
                "unresolved" => ReportResultFilter.NeedsDeviceResolution,
                _ => ReportResultFilter.Any,
            },

            ContainerId = container,

            // 🔴 الحدود بأيام القاهرة — نفس اليوم اللي بيتعرض
            // للمستخدم.
            FromUtc = CairoDay.StartUtc(from),
            ToUtc = CairoDay.AfterUtc(to),

            Page = p,
            PageSize = size,
        };
    }
}
