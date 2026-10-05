using Codlek.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Api.Extensions;

/// <summary>بيحوّل الفشل لرد HTTP قياسي (ProblemDetails).</summary>
public static class ResultExtensions
{
    public static ObjectResult ToProblem(this Result result)
    {
        // ⚠️ نجاح بيتحوّل لمشكلة = غلط في الكنترولر نفسه، مش في
        // البيانات. فالاستثناء هو الصح — بيبان في أول تجربة.
        if (result.IsSuccess)
            throw new InvalidOperationException("نتيجة ناجحة ماتتحوّلش لمشكلة.");

        int statusCode = result.Error.StatusCode ?? 400;

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = result.Error.Description,
        };

        /*
          ⚠️ **شكل `errors` واحد في الحالتين.**

          التحقق بيدّي قاموس «الحقل ← الرسائل»، والفشل العادي بيدّي
          سبب واحد. ولو الشكل اتغيّر على حسب النوع، الواجهة كانت لازم
          تتعامل مع شكلين — وأول واحد يتنسى بيطلّع شاشة فاضية.
        */
        problem.Extensions["errors"] = result.Error is ValidationError validation
            ? validation.Errors
            : new Dictionary<string, string[]>
            {
                [result.Error.Code] = [result.Error.Description],
            };

        /*
          🔴 **`message` — اللي اللوحة بتقراه فعلاً.**

          اللوحة بتطلّع رسالة الخطأ من `parsed.message` (زي ما القديم
          بيرجّع `{ message }`)، ومن غير الحقل ده كل رسالة عربي محدّدة
          — «الكود ده اتفعّلت بيه محطة فعلاً» — كانت بتبقى «لم تنجح
          العملية.» بعد التحويل. اتلقط بتشغيل فحوص القديم على الجديد.

          ⚠️ وفي التحقق، الرسالة هي **أول خطأ محدّد** مش الوصف العام —
          القديم كان بيرجّع «اسم المستخدم قصير» مش «البيانات فيها غلط».
        */
        problem.Extensions["message"] = MessageOf(result.Error);

        return new ObjectResult(problem) { StatusCode = statusCode };
    }

    /// <summary>الرسالة اللي بتتعرض للمستخدم — <b>الأكثر تحديداً</b>.</summary>
    public static string MessageOf(Error error) =>
        error is ValidationError validation
            ? validation.Errors.Values.SelectMany(m => m)
                .FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? error.Description
            : error.Description;
}
