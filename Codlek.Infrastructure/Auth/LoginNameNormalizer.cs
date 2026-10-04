using Codlek.Core.Text;
using Microsoft.AspNetCore.Identity;

namespace Codlek.Infrastructure.Auth;

/// <summary>
/// بيخلّي Identity تطبّع أسماء الدخول <b>بقاعدة المشروع</b>.
///
/// <para>🔴 <b>من غير الكلاس ده بيبقى فيه مفتاحين لنفس الاسم.</b>
/// Identity الافتراضية بتعمل <c>ToUpperInvariant</c> وبتحطّه في
/// <c>NormalizedUserName</c>. والمشروع (والبرنامج المكتبي على الراكة)
/// بيستعملوا <see cref="LoginName.Normalize"/> — حروف <b>صغيرة</b>
/// ومقصوصة على ٦٠.</para>
///
/// <para>⚠️ <b>والنتيجة لو اتسابوا مختلفين:</b> فحص «الاسم ده متاخد؟»
/// بيقارن مفتاح بمفتاح تاني، فبيرجّع «فاضي» دايماً — والفهرس الفريد
/// هو اللي بيرمي عند الحفظ. المدير بيشوف استثناء قاعدة بيانات خام بدل
/// رسالة. وأسوأ: الراكة بتوصل لمفتاح والموقع بيوصل لمفتاح تاني، فاسم
/// يدخل على الراكة ومايدخلش على الموقع.</para>
///
/// <para>⚠️ <b>والبريد بيفضل بالشكل الافتراضي (كابيتال).</b> قاعدة
/// <c>LoginName</c> بتقص على ٦٠ حرف — وده صح لاسم مستخدم وغلط لبريد.
/// والبريد مش مفتاح دخول في النظام ده أصلاً.</para>
///
/// <para>🔴 <b>وقت التحويل:</b> الحسابات المنقولة لازم يتحسب لها
/// <c>NormalizedUserName</c> من جديد بالقاعدة دي — مش يتنقل من العمود
/// القديم ولا من Identity الافتراضية.</para>
/// </summary>
public sealed class LoginNameNormalizer : ILookupNormalizer
{
    public string? NormalizeName(string? name) => LoginName.Normalize(name);

    /// <summary>
    /// ⚠️ البريد بالشكل الافتراضي — <c>UpperInvariant</c> من غير قص.
    /// </summary>
    public string? NormalizeEmail(string? email) => email?.Trim().ToUpperInvariant();
}
