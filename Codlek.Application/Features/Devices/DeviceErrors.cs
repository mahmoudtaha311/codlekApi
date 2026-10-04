using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Devices;

/// <summary>
/// أسباب فشل نقط الأجهزة.
///
/// <para>⚠️ <b>والمسح ليه سببين مختلفين تحت نفس <c>404</c>.</b>
/// القديم كان بيرجّع حقل <c>reason</c> في الجسم
/// (<c>"shape"</c>/<c>"notfound"</c>)، واللي اتنقل هنا هو <b>كود
/// الخطأ</b> — نفس التفريق بشكل المشروع ده.</para>
///
/// <para>⚠️ <b>واللوحة النهاردة بتجمعهم في رسالة واحدة:</b> بتبص
/// على <c>404</c> وبتقول «لا يوجد جهاز بالكود كذا». فالتفريق
/// سيرفري بحت — بس هو معلومة حقيقية للتشخيص: «الفني بيمسح QR بتاع
/// حاجة تانية» مشكلة مختلفة تماماً عن «الكود مش مسجّل».</para>
/// </summary>
public static class DeviceErrors
{
    public static readonly Error NotFound =
        new("device.not_found", "اللاب ده مش موجود", 404);

    /// <summary>
    /// اللي اتمسح <b>مش كود لاب خالص</b> — نص عشوائي، أو رابط، أو
    /// معرّف فحص.
    /// </summary>
    public static Error CodeShape(string? raw) =>
        new("device.code_shape",
            raw is { Length: > 0 }
                ? "القيمة دي مش كود لاب"
                : "اكتب كود اللاب أو امسحه",
            404);

    /// <summary>
    /// الشكل صح — بس مفيش لاب بالكود ده في الشركة، <b>ولا دلوقتي
    /// ولا قبل كده</b>.
    /// </summary>
    public static Error CodeNotFound(string code) =>
        new("device.code_not_found",
            $"مفيش لاب بالكود {code} — لا دلوقتي ولا قبل كده", 404);
}
