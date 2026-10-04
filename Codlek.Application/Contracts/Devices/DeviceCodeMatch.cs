namespace Codlek.Application.Contracts.Devices;

/// <summary>
/// لاب طلع من المسح بكود — <b>والكود ده لسه بتاعه ولا لأ</b>.
/// </summary>
/// <param name="IsCurrent">
/// 🔴 <c>true</c> = ده كوده الحالي. <c>false</c> = الكود ده عدّى عليه
/// قبل كده ومتسجّل كمرساة تاريخية، ودلوقتي ليه كود تاني.
///
/// <para>⚠️ والفرق ده بيتعرض للمستخدم: استيكر قديم في إيده لازم
/// يوصّله للاب، بس لازم يعرف إن الاستيكر مش الكود الحالي.</para>
/// </param>
public sealed record DeviceCodeMatch(Guid DeviceId, string PublicCode, bool IsCurrent);
