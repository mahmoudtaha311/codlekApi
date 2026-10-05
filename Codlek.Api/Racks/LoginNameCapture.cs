using System.Text.Json;
using Codlek.Core.Text;

namespace Codlek.Api.Racks;

/// <summary>
/// بيقرا اسم المستخدم من طلب دخول اللوحة <b>قبل حدّ الطلبات</b>.
///
/// <para>🔴 <b>ليه خطوة لوحدها:</b> حدّ الطلبات بيقسّم على (IP + الاسم)
/// زي القديم بالظبط، بس دالة التقسيم متزامنة — مش بتقدر تقرا جسم JSON.
/// فالخطوة دي بتقراه مرة، وبترجّع المجرى لأوله عشان الربط يلاقيه كامل،
/// وبتسيب الاسم المطبَّع في <c>HttpContext.Items</c>.</para>
///
/// <para>⚠️ <b>وبتقرا ٤ كيلو بالكتير.</b> طلب الدخول اسم وباسورد؛ أي
/// حاجة أكبر مش طلب دخول، ومش هنصرف ذاكرة على قرايتها هنا.</para>
/// </summary>
public static class LoginNameCapture
{
    public const string Path = "/api/v1/auth/login";

    private const string ItemKey = "codlek.login-name";

    private const int MaxBytes = 4 * 1024;

    /// <summary>الاسم المطبَّع اللي اتقرا — أو فاضي.</summary>
    public static string From(HttpContext http) =>
        http.Items[ItemKey] as string ?? "";

    public static IApplicationBuilder UseLoginNameCapture(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var request = context.Request;

            if (HttpMethods.IsPost(request.Method)
                && string.Equals(request.Path.Value?.TrimEnd('/'), Path, StringComparison.OrdinalIgnoreCase)
                && request.ContentLength is null or <= MaxBytes)
            {
                request.EnableBuffering(MaxBytes);

                try
                {
                    using var document = await JsonDocument.ParseAsync(
                        request.Body, cancellationToken: context.RequestAborted);

                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "username", StringComparison.OrdinalIgnoreCase)
                            && property.Value.ValueKind == JsonValueKind.String)
                        {
                            context.Items[ItemKey] = LoginName.Normalize(property.Value.GetString());
                            break;
                        }
                    }
                }
                catch (JsonException)
                {
                    // ⚠️ جسم بايظ: الربط هيرفضه بنفسه — الحد بيقسّم على الـIP بس.
                }
                finally
                {
                    request.Body.Position = 0;
                }
            }

            await next();
        });
}
