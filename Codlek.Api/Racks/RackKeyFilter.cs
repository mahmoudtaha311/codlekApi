using Codlek.Application.Interfaces;
using Codlek.Core.Entities;
using Codlek.Core.Racks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Codlek.Api.Racks;

/// <summary>
/// حارس مفتاح المحطة — <b>بدل <c>[Authorize]</c></b>.
///
/// <para>🔴 <b>وليه فلتر مش مخطّط تحقّق (<c>AuthenticationScheme</c>).</b>
/// سطح الراكة مالوش هوية مستخدم ومالوش مطالبات ومالوش دور: هو
/// مفتاح جهاز. ولو اتعمل كمخطّط، كل حاجة في الأنبوب هتفتكر إن فيه
/// <c>User</c> — والـ<c>ICurrentUser</c> بترمي أول ما تتنده على
/// طلب راكة لأن مفيش مطالبة شركة.</para>
///
/// <para>🔴 <b>والرد <c>401</c> بجسم <u>فاضي</u> — مش
/// <c>ProblemDetails</c>.</b> ده اللي القديم بيرجّعه، والراكة
/// بتفرّق بين الحالات بالرقم بس. و<c>ProblemDetails</c> هنا معناه
/// جسم JSON فيه <c>title</c> — والراكة بتحاول تقرا منه
/// <c>message</c> ومابتلاقيهوش.</para>
///
/// <para>⚠️ <b>ولا <c>403</c> ولا رسالة.</b> مفتاح غلط، ومحطة
/// موقوفة، ومحطة ملغية — كلهم <c>401</c> فاضية. اللي بيحاول مالوش
/// يعرف إيه اللي ناقص.</para>
///
/// <para>⚠️ <b>والمحطة بتتحط في <c>HttpContext.Items</c></b> عشان
/// النقطة تقراها من غير ما تعمل التحقق تاني — تحقق تشفيري مرتين
/// على نفس الطلب شغل مضاعف على كل مزامنة.</para>
/// </summary>
public sealed class RackKeyFilter(IRackAuthenticator racks) : IAsyncActionFilter
{
    /// <summary>مفتاح المحطة المتحققة في <c>HttpContext.Items</c>.</summary>
    public const string ItemKey = "codlek.rack";

    public async Task OnActionExecutionAsync(
        ActionExecutingContext context, ActionExecutionDelegate next)
    {
        string? key = context.HttpContext.Request.Headers[RackKey.Header];

        var rack = await racks.AuthenticateAsync(key, context.HttpContext.RequestAborted);

        if (rack is null)
        {
            /*
              🔴 **جسم فاضي بالظبط.**

              `UnauthorizedResult` بيكتب الحالة وبس — من غير جسم ومن
              غير `Content-Type`. وده المطلوب: أي جسم HTML هنا
              كارثة، لأن الراكة بتعتبر الرد اللي شكله نجاح نجاحاً
              **وبتمسح الصف من طابورها**.
            */
            context.Result = new UnauthorizedResult();
            return;
        }

        context.HttpContext.Items[ItemKey] = rack;

        await next();
    }
}

/// <summary>
/// ⚠️ <b>سمة عشان الفلتر يتحط على الكنترولر بسطر واحد.</b> والفلتر
/// بياخد خدماته من الحاقن، فالسمة <c>ServiceFilter</c> مش
/// <c>TypeFilter</c> بمعاملات.
/// </summary>
public sealed class RackKeyAttribute : ServiceFilterAttribute
{
    public RackKeyAttribute() : base(typeof(RackKeyFilter))
    {
    }
}

/// <summary>
/// بيقرا المحطة المتحققة من الطلب.
///
/// <para>⚠️ <b>بترمي لو مفيش محطة.</b> الوصول للقيمة دي معناه إن
/// الفلتر عدّى، فغيابها عيب برمجة (نقطة من غير
/// <c>[RackKey]</c>) — مش حالة بيانات. والاستثناء بيبان في أول
/// تجربة، بخلاف <c>null</c> اللي بيعدّي ويطلّع رد ناقص.</para>
/// </summary>
public static class RackRequest
{
    public static Rack Rack(this HttpContext context) =>
        context.Items[RackKeyFilter.ItemKey] as Rack
        ?? throw new InvalidOperationException(
            "النقطة دي محتاجة [RackKey] — مفيش محطة متحققة في الطلب.");
}
