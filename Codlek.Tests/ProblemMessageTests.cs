using System.Text.Json;
using Codlek.Api.Extensions;
using Codlek.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Codlek.Tests;

/// <summary>
/// كل رد خطأ فيه <c>message</c> — <b>اللي اللوحة بتقراه فعلاً</b>.
///
/// <para>🔴 <b>اتلقط بتشغيل فحوص القديم على الجديد.</b> اللوحة بتطلّع
/// الرسالة من <c>parsed.message</c>؛ والجديد كان بيرجّع
/// <c>ProblemDetails</c> بالرسالة في <c>title</c> بس — فكل رسالة عربي
/// محدّدة كانت هتبقى «لم تنجح العملية.» بعد التحويل.</para>
/// </summary>
public class ProblemMessageTests
{
    private static JsonElement Body(Result result)
    {
        var problem = Assert.IsType<ProblemDetails>(result.ToProblem().Value);

        return JsonSerializer.SerializeToElement(
            problem, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public void A_business_error_carries_its_description_as_message()
    {
        var body = Body(Result.Failure(
            new Error("rack.code_consumed", "الكود ده اتفعّلت بيه محطة فعلاً", 400)));

        Assert.Equal("الكود ده اتفعّلت بيه محطة فعلاً", body.GetProperty("message").GetString());
        Assert.Equal("الكود ده اتفعّلت بيه محطة فعلاً", body.GetProperty("title").GetString());
        Assert.Equal(400, body.GetProperty("status").GetInt32());
    }

    /// <summary>
    /// ⚠️ <b>في التحقق، الرسالة هي أول خطأ محدّد</b> — مش «البيانات فيها
    /// غلط». القديم كان بيقول للمستخدم إيه بالظبط اللي غلط.
    /// </summary>
    [Fact]
    public void A_validation_error_carries_the_first_specific_message()
    {
        var body = Body(Result.Failure(new ValidationError(new Dictionary<string, string[]>
        {
            ["Username"] = ["", "اسم المستخدم قصير."],
            ["DisplayName"] = ["الاسم الظاهر مطلوب."],
        })));

        Assert.Equal("اسم المستخدم قصير.", body.GetProperty("message").GetString());

        // والقاموس الكامل لسه موجود للي محتاجه.
        Assert.True(body.GetProperty("errors").TryGetProperty("DisplayName", out _));
    }

    [Fact]
    public void A_validation_error_with_no_text_falls_back_to_the_description()
    {
        var error = new ValidationError(new Dictionary<string, string[]> { ["X"] = ["  "] });

        var body = Body(Result.Failure(error));

        Assert.Equal(error.Description, body.GetProperty("message").GetString());
    }
}
