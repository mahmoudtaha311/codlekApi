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
/// <c>ProblemDetails</c>.</b> ده اللي القديم بيرجّعه بالحرف، وفحص
/// العقد في القديم بيجمّده (<c>Assert.Equal(string.Empty, body)</c>).
/// والراكة بتفرّق بين الحالات <b>بالرقم بس</b>.</para>
///
/// <para>⚠️ <b>والراكة مابتقراش جسم الـ<c>401</c> على المسارات
/// المفتاحية خالص</b> — قارئ <c>message</c> الوحيد عندها في مسار
/// التسجيل اللي مالوش مفتاح. فالخطر الحقيقي من
/// <c>ProblemDetails</c> هنا هو كسر العقد المجمّد، مش رسالة ضايعة.
/// (تعليق قديم هنا كان بيقول إن الراكة بتدوّر على <c>message</c>
/// في الرد ده — ده مش صح.)</para>
///
/// <para>🔴 <b>والخطر اللي بيضيّع شغل فعلاً حاجة تانية:</b>
/// <c>[Authorize]</c> مع معالج كوكي بيحوّل الرفض لـ<c>302</c> على
/// صفحة الدخول، والراكة بتمشي ورا التحويل افتراضياً فبتستلم
/// <c>200</c> + HTML — وبتقراه <b>نجاح</b> وبتمسح الصف من
/// طابورها.</para>
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
              🔴 **جسم فاضي بالظبط — والسطرين دول مقاسين.**

              `UnauthorizedResult` **مابيدّيش** جسم فاضي في مشروع
              كنترولرز: هو `IStatusCodeActionResult`، و`[ApiController]`
              بيحوّله تلقائياً لـ`ProblemDetails`
              (`ApiBehaviorOptions.ClientErrorMapping`) — فالراكة
              بتاخد ١٦٥ بايت JSON فيها `type` و`title` و`traceId`.
              لقينا ده بضرب حقيقي على HTTP؛ فحوص الوحدة كانت بتشوف
              النتيجة قبل الأنبوب فعدّت كلها خضرا.

              و`EmptyResult` مش `IStatusCodeActionResult`، فالتحويل
              مابيلمسهوش — والحالة بتتكتب بالإيد.

              ⚠️ والفرق ده مجمّد في فحص عقد في القديم
              (`Assert.Equal(string.Empty, body)`).
            */
            context.HttpContext.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            context.Result = new EmptyResult();
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
