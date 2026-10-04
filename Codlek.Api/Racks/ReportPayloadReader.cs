using System.Text.Json;
using Codlek.Application.Contracts.Sync;
using Codlek.Application.Features.Rack.IngestReports;

namespace Codlek.Api.Racks;

/// <summary>
/// بيقرا حمولة الفحوص من جسم الطلب.
///
/// <para>🔴 <b>والقراية من المجرى مباشرةً — مش نسخة نصية.</b> النسخة
/// النصية بتعمل نسخة كاملة من الجسم في الذاكرة، وبعدها شجرة كائنات
/// فوقها: مع دفعة كبيرة الذاكرة بتوصل <b>٣–٤ أضعاف</b> حجم الطلب،
/// والخدمة على استضافة ذاكرتها نص جيجا.</para>
///
/// <para>⚠️ <b>والشكلين مقبولين:</b> مصفوفة مباشرة <c>[...]</c>،
/// أو الغلاف الكامل <c>{ Version, Reports: [...] }</c> — عشان
/// مانوقّفش المدير على تفصيلة زي دي.</para>
/// </summary>
public static class ReportPayloadReader
{
    /// <summary>
    /// بيرجّع الفحوص، أو بيرمي.
    ///
    /// <para>⚠️ <b>الحمولة الأكبر من الحد بترمي
    /// <c>BadHttpRequestException</c></b> من جوّه
    /// <see cref="LimitedStream"/> — والكنترولر بيحوّلها
    /// <c>413</c>.</para>
    /// </summary>
    public static async Task<List<LaptopReportPayload>> ReadAsync(
        Stream body, long maxBytes, CancellationToken ct = default)
    {
        // 🔴 حزام أمان: لو الترويسة كدبت في حجمها، القراية نفسها
        //    بتتوقف.
        Stream guarded = new LimitedStream(body, maxBytes);

        /*
          ⚠️ **بنقرا أول حرف غير فاضي عشان نعرف الشكل.**

          وجسم الطلب مش بيقبل الرجوع للخلف، فاللي اتقرا بيترجّع قدام
          الباقي بـ`PrefixedStream` بدل `Seek`.
        */
        var consumed = new List<byte>(8);
        var single = new byte[1];
        int first = -1;

        while (await guarded.ReadAsync(single.AsMemory(0, 1), ct) == 1)
        {
            consumed.Add(single[0]);

            if (char.IsWhiteSpace((char)single[0])) continue;

            first = single[0];
            break;
        }

        // ⚠️ جسم فاضي = صفر فحوص، مش خطأ. الراكة بتبعت دفعة فاضية
        //    وقت ما الطابور يفضى بينها وبين الإرسال.
        if (first < 0) return [];

        var replay = new PrefixedStream([.. consumed], guarded);

        if (first == '[')
        {
            return await JsonSerializer.DeserializeAsync<List<LaptopReportPayload>>(
                replay, IngestReportsCommandHandler.Json, ct) ?? [];
        }

        var file = await JsonSerializer.DeserializeAsync<ReportFilePayload>(
            replay, IngestReportsCommandHandler.Json, ct);

        return file?.Reports ?? [];
    }
}
