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

        return new ObjectResult(problem) { StatusCode = statusCode };
    }
}
