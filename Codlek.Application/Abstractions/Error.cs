namespace Codlek.Application.Abstractions;

/// <summary>
/// سبب فشل — <b>كود للآلة ورسالة للبني آدم</b>.
///
/// <para>🔴 <b>الكود هو اللي بيتفرّع عليه، مش الرسالة.</b> الراكة
/// والداش بورد بيتصرّفوا حسب <see cref="Code"/>؛ والرسالة للعرض بس
/// وممكن تتظبّط صياغتها في أي وقت من غير ما حاجة تتكسر.</para>
///
/// <para>⚠️ و<see cref="StatusCode"/> جزء من السبب مش قرار الكنترولر.
/// «الباسورد غلط» ٤٠١ في أي مكان تتنده منه، و«مش موجود» ٤٠٤ — فالحتة
/// اللي بتعرف المعنى هي اللي بتقول الكود.</para>
/// </summary>
public record Error(string Code, string Description, int? StatusCode)
{
    public static readonly Error None = new(string.Empty, string.Empty, null);
}
