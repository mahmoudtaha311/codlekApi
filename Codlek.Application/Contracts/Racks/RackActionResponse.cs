namespace Codlek.Application.Contracts.Racks;

/// <summary>
/// رد إجراء على محطة — <b>رسالة وبس</b>.
///
/// <para>⚠️ <b>والشكل ده مقصود:</b> اللوحة بتقرا <c>message</c>
/// وبتعرضها زي ما هي. الرد اللي كان بيرجّع الصف كامل كان بيخلّي
/// الإيقاف باب تاني على بيانات المحطة.</para>
/// </summary>
public sealed record RackActionResponse(string Message);
