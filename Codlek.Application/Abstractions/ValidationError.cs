namespace Codlek.Application.Abstractions;

/// <summary>
/// سبب فشل من التحقق — <b>ومعاه الحقول اللي غلط بالاسم</b>.
///
/// <para>⚠️ <b>ليه نوع مخصوص وهو ينفع يبقى <see cref="Error"/> عادي.</b>
/// الرد لازم يقول «الاسم مطلوب» جمب خانة الاسم، مش رسالة واحدة فوق.
/// والقايمة دي هي اللي بتخلّي ده ممكن.</para>
/// </summary>
public sealed record ValidationError(IDictionary<string, string[]> Errors)
    : Error("validation.failed", "البيانات المرسلة فيها نقص أو غلط.", 400);
