using System.Security.Claims;
using Codlek.Application.Interfaces;
using Codlek.Core.Enums;

namespace Codlek.Api.Services;

/// <summary>
/// المستخدم الحالي من التوكن.
///
/// <para>🔴 <b>الشركة بتتقرا من ادعاء <c>tenant</c>، وعمرها ما
/// بتتقرا من الطلب.</b> لو جت من الطلب، مدير شركة كان هيقدر يقرا
/// ويعدّل بيانات شركة تانية بإنه يبعت معرّف مختلف — ومفيش سجل كان
/// هيبيّن ده.</para>
///
/// <para>⚠️ <b>والكلاس ده بيرمي لو مفيش مستخدم.</b> مرجع
/// <c>Guid.Empty</c> كان أخطر: الاستعلام هيعدّي ويرجّع صفر صفوف —
/// يعني شاشة فاضية بدل رسالة «سجّل دخول». والأسوأ إن أي
/// <c>Add</c> هيكتب صف بشركة <c>Guid.Empty</c>، وده شغل بيضيع في
/// مكان محدش بيبص فيه.</para>
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid Id => Required("sub", out Guid id) ? id : throw NotSignedIn();

    public Guid TenantId => Required("tenant", out Guid id) ? id : throw NotSignedIn();

    public string DisplayName => Principal?.FindFirstValue("display") ?? "";

    public string Code => Principal?.FindFirstValue("code") ?? "";

    public UserRole Role =>
        Enum.TryParse(Principal?.FindFirstValue(ClaimTypes.Role), out UserRole role)
            ? role
            // ⚠️ أقل صلاحية هي الافتراضي. دور مش مفهوم مايتحوّلش
            // لمدير — والسياسات بتترفض عند الحاجز قبل ما توصل هنا.
            : UserRole.Technician;

    private bool Required(string claim, out Guid value) =>
        Guid.TryParse(Principal?.FindFirstValue(claim), out value);

    private static InvalidOperationException NotSignedIn() =>
        new("الطلب ده مالوش مستخدم — نقطة محتاجة تحقق اتنادت من غير [Authorize].");
}
