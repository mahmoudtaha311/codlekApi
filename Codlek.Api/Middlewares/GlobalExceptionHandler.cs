using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Middlewares;

/// <summary>
/// آخر خط — <b>أي استثناء مامسكهوش حد</b>.
///
/// <para>🔴 <b>تفاصيل الاستثناء بتروح للسجل بس، مش للرد.</b> رسالة
/// الاستثناء فيها أسماء جداول وأعمدة وساعات حتة من نص الاتصال.
/// وده بيتكتب في صفحة الخطأ عند أي حد.</para>
///
/// <para>⚠️ ومعرّف الطلب راجع في الرد: اللي بيشتكي بيقول الرقم ده،
/// واللي بيدوّر بيلاقي السطر بالظبط في السجل.</para>
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> log)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        string traceId = context.TraceIdentifier;

        log.LogError(exception,
            "عطل مامسكهوش حد في {Method} {Path} — {TraceId}",
            context.Request.Method, context.Request.Path, traceId);

        const string Message = "في مشكلة في السيرفر. حاول تاني، ولو فضلت كلّم الدعم.";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = Message,

            // ⚠️ `message` عشان اللوحة — نفس الحقل اللي `ToProblem`
            //    بيحطّه. من غيره المستخدم بيشوف «لم تنجح العملية.»
            Extensions = { ["traceId"] = traceId, ["message"] = Message },
        };

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
