namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// جسم نقطة إضافة ملاحظة على لاب.
///
/// <para>⚠️ <b>النص وبس.</b> الكاتب والشركة والوقت من السيرفر — وأي
/// حقل تاني في الجسم بيتجاهل.</para>
///
/// <para>⚠️ و<c>Body</c> لازم يفضل <c>string?</c>: <c>null</c> في
/// JSON بيعدّي على عدم القابلية للعدم أصلاً، والمعالج بيعامله
/// كفاضي.</para>
/// </summary>
public sealed record AddDeviceNoteRequest(string? Body);
