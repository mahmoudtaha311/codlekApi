using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Paging;
using Codlek.Core.Text;
using Codlek.Core.Time;

namespace Codlek.Application.Features.Devices;

/// <summary>
/// بناء فلتر الأجهزة — <b>مكان واحد للقايمة وللتصدير</b>.
///
/// <para>🔴 <b>وده مش ترتيب كود، ده صحّة.</b> الزرار في الواجهة
/// مكتوب فوقه «اللي مفلتر على الشاشة مفلتر في الإكسل»، وفي القديم
/// ماكانش صح: تلات فلاتر كانوا ناقصين من نسخة التصدير — فالمدير
/// يفلتر على «مخزن الجاهز» ويصدّر فيطلعله <b>كل</b>
/// الأجهزة.</para>
///
/// <para>⚠️ <b>وكل قيمة مش مفهومة بتتجاهل.</b> دي مدخلات من رابط
/// محفوظ في المتصفح — والوقوف عليها بيحوّل الرابط القديم لصفحة
/// خطأ.</para>
/// </summary>
internal static class DeviceFilters
{
    public static DeviceListFilter Build(
        string? search,
        string? status,
        string? confidence,
        string? outcome,
        string? technician,
        Guid? rack,
        DateTime? from,
        DateTime? to,
        string? stage,
        string? sort,
        Guid? container,
        string? flag,
        string? handover,
        Guid? location,
        int? page = null,
        int? pageSize = null)
    {
        var (p, size) = Paging.Clamp(page, pageSize);

        string raw = (search ?? "").Trim();
        var wantedStatus = Parse<DeviceLifecycleStatus>(status);

        return new DeviceListFilter
        {
            /*
              🔴 **المدموج داخل لو المستخدم بيدوّر أو طالب الحالة
              صراحةً.**

              الكود المتقاعد مطبوع على ليبل ملزوق على لاب حقيقي،
              والفني اللي بيدوّر بيه لازم يوصل للجهاز الكانوني مش
              لصفحة فاضية.
            */
            IncludeMerged = wantedStatus == DeviceLifecycleStatus.Merged || raw.Length > 0,

            ExactCode = raw.Length == 0 ? null : raw,

            // ⚠️ توحيد **تقني** للسيريال — مش التوحيد العربي.
            IdentityValue = raw.Length == 0 ? null : IdentityValues.Normalize(raw),

            SearchPattern = raw.Length == 0
                ? null
                : SearchPattern.Contains(ArabicText.Normalize(raw)),

            Status = wantedStatus,
            Confidence = Parse<DeviceIdentityConfidence>(confidence),

            TechnicianCode = string.IsNullOrWhiteSpace(technician) ? null : technician.Trim(),
            RackId = rack,
            ContainerId = container,
            Stage = Parse<DeviceOperationalStage>(stage),

            Flag = (flag ?? "").Trim().ToLowerInvariant() switch
            {
                "partchanged" => DeviceAttentionFlag.PartChanged,
                "duplicate" => DeviceAttentionFlag.DuplicateSuspected,
                "attention" => DeviceAttentionFlag.Attention,
                _ => DeviceAttentionFlag.Any,
            },

            Handover = (handover ?? "").Trim().ToLowerInvariant() switch
            {
                "yes" => DeviceHandoverFilter.HandedOver,
                "no" => DeviceHandoverFilter.InWorkshop,
                _ => DeviceHandoverFilter.Any,
            },

            LocationId = location,

            // 🔴 الحدود بأيام القاهرة، والمدى نصف مفتوح.
            FromUtc = CairoDay.StartUtc(from),
            ToUtc = CairoDay.AfterUtc(to),

            Outcome = (outcome ?? "").Trim().ToLowerInvariant() switch
            {
                "failures" => DeviceOutcomeFilter.Failures,
                "errors" => DeviceOutcomeFilter.Errors,
                "clean" => DeviceOutcomeFilter.Clean,
                "never" => DeviceOutcomeFilter.NeverTested,
                _ => DeviceOutcomeFilter.Any,
            },

            OldestStageFirst = string.Equals(
                sort, "oldest-stage", StringComparison.OrdinalIgnoreCase),

            Page = p,
            PageSize = size,
        };
    }

    /// <summary>
    /// ⚠️ <b>بالاسم، وبحالة أحرف متجاهلة.</b> والقيمة المش مفهومة
    /// بترجع <c>null</c> — يعني «مفيش فلتر»، مش <c>400</c>.
    /// </summary>
    private static T? Parse<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
