using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces;

/// <summary>
/// المستخدم الحالي — <b>مقروء من التوكن، مش من الطلب</b>.
///
/// <para>🔴 <b>الشركة بتيجي من هنا دايماً.</b> لو جت من الطلب، مدير
/// شركة كان هيقدر يقرا ويعدّل بيانات شركة تانية بإنه يبعت
/// <c>tenantId</c> مختلف. والطلب ده مش هيبان غلط في أي سجل.</para>
///
/// <para>⚠️ وده السبب إن الواجهة دي موجودة بدل ما كل Handler يقرا
/// <c>HttpContext</c>: طبقة التطبيق مالهاش علاقة بـHTTP، ولو كل
/// واحد قرا لوحده كان أول واحد ينسى الترشيح بالشركة يفتح البيانات
/// كلها.</para>
/// </summary>
public interface ICurrentUser
{
    Guid Id { get; }
    Guid TenantId { get; }
    string DisplayName { get; }
    string Code { get; }
    UserRole Role { get; }

    /// <summary>فيه مستخدم أصلاً؟ — <c>false</c> يعني طلب مجهول.</summary>
    bool IsAuthenticated { get; }
}
