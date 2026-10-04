namespace Codlek.Application.Contracts.Account;

/// <summary>
/// تعديل الملف الشخصي — <b>الاسم وبس</b>.
///
/// <para>🔴 <b>مفيش دور ولا شركة ولا كود ولا صلاحية هنا عن قصد.</b>
/// العقد اللي بياخد الحقول دي بيخلّي «عدّل حسابي» و«ارفع نفسي لمالك»
/// نفس الطلب، والفرق بينهم سطر تحقق واحد. وأي غلطة في السطر ده بتبقى
/// ترقية صلاحيات.</para>
/// </summary>
public sealed record UpdateProfileRequest(string DisplayName);
