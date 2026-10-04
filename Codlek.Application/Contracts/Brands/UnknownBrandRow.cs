namespace Codlek.Application.Contracts.Brands;

/// <summary>
/// اسم ماركة موجود في الأجهزة ومش في القايمة.
///
/// <para>⚠️ بيتعرض بعدده عشان صاحب الشغل يبدأ بالأكتر — القايمة
/// الطويلة من غير ترتيب بتتساب.</para>
/// </summary>
public sealed record UnknownBrandRow(string Name, int DeviceCount);
