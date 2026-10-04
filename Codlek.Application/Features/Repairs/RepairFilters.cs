using Codlek.Application.Interfaces.Repositories;
using Codlek.Core.Enums;
using Codlek.Core.Paging;
using Codlek.Core.Text;
using Codlek.Core.Time;

namespace Codlek.Application.Features.Repairs;

/// <summary>
/// بناء فلتر الصيانة — <b>مكان واحد للقايمة وللتصدير</b>.
///
/// <para>🔴 <b>وده مش ترتيب كود، ده صحّة.</b> الملف اللي بيطلع
/// بصفوف غير اللي قدام المدير أسوأ من مفيش ملف — هو بيقارنهم
/// وبيلاقي فرق مالوش تفسير. ونفس الحكاية دي حصلت فعلاً في قايمة
/// الأجهزة في القديم: الفلتر كان مكتوب مرتين وتلاتة منه كانوا
/// ناقصين من نسخة التصدير.</para>
///
/// <para>⚠️ <b>والمستودع مابيشوفش نص المستخدم الخام.</b> الحالة
/// بتتحوّل لـenum، والتاريخ لـUTC، ونص البحث بيتوحّد ويتهرّب — كل ده
/// هنا. ولو المستودع عمل الكلام ده، كان لازم يستعمل
/// <c>ArabicText.Normalize</c> جوّه <c>Where</c> — وده بيترجم
/// وبيرمي على قاعدة حقيقية.</para>
/// </summary>
internal static class RepairFilters
{
    /// <summary>⚠️ القيمة الوحيدة اللي بتقلب الترتيب. أي حاجة تانية = الأحدث.</summary>
    private const string OldestKey = "oldest";

    public static RepairListFilter Build(
        string? search,
        string? status,
        string? approval,
        Guid? technician,
        DateTime? from,
        DateTime? to,
        string? sort,
        int? page = null,
        int? pageSize = null)
    {
        var (p, size) = Paging.Clamp(page, pageSize);

        string raw = (search ?? "").Trim();

        return new RepairListFilter
        {
            /*
              ⚠️ **الكود بيتقارن خام، ونص البحث بيتوحّد.**

              كود الأمر لاتيني (`RP-00000123`) فالتوحيد العربي مالوش
              لازمة عليه؛ ونص البحث لازم يتوحّد عشان «أحمد» و«احمد»
              يلاقوا نفس الصف.
            */
            ExactCode = raw.Length == 0 ? null : raw,

            SearchPattern = raw.Length == 0
                ? null
                : SearchPattern.Contains(ArabicText.Normalize(raw)),

            /*
              🔴 **الفلتر المش مفهوم بيتجاهل — مابيرفضش.**

              `TryParse` بيفشل فالقيمة بتبقى `null`، يعني «مفيش
              فلتر». ولو رجّعنا ٤٠٠، رابط محفوظ فيه حالة قديمة كان
              بيفضّي الشاشة والمدير مش عارف ليه.
            */
            Status = Parse<RepairStatus>(status),
            Approval = Parse<RepairApproval>(approval),

            TechnicianId = technician,

            /*
              🔴 **المدى نصف مفتوح وبيحترم التوقيت الصيفي.**

              `CairoDay` بتحسب بداية اليوم بتوقيت القاهرة الحقيقي،
              مش بـ+٢ ثابتة. ومصر بتقدّم الساعة **نص الليل**، فاليوم
              اللي بيتقدّم فيه مالوش نص ليل أصلاً.
            */
            FromUtc = CairoDay.StartUtc(from),
            ToUtc = CairoDay.AfterUtc(to),

            Oldest = string.Equals(sort, OldestKey, StringComparison.OrdinalIgnoreCase),

            Page = p,
            PageSize = size,
        };
    }

    /// <summary>
    /// ⚠️ <b>بالاسم مش بالرقم.</b> الداش بورد بتبعت
    /// <c>status=InProgress</c>؛ و<c>TryParse</c> بيقبل الأرقام
    /// كمان، فـ<c>status=99</c> بيعدّي كـ<c>(RepairStatus)99</c>
    /// ويرجّع قايمة فاضية بدل ما يتجاهل. ومنقول زي ما هو — القديم
    /// كان بيعمل نفس الحاجة، والقايمة الفاضية مش ضرر.
    /// </summary>
    private static T? Parse<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : null;
}
