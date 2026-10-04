namespace Codlek.Application.Contracts.Analytics;

/// <summary>
/// أجهزة جديدة مقابل إعادة فحص.
///
/// <para>🔴 <b>الجهاز «جديد» لو أول فحص ليه <u>على الإطلاق</u> وقع
/// جوّه الفترة.</b> التجميع بيتعمل على كل تاريخ الجهاز مش على
/// الفترة — والفرق ده هو المقصود: جهاز اتفحص خمس مرات الشهر ده
/// وأول مرة كانت السنة اللي فاتت هو «إعادة فحص»، مش خمس أجهزة
/// جديدة.</para>
/// </summary>
/// <param name="Unlinked">
/// ⚠️ فحوص لسه مش مربوطة بجهاز — لا جديدة ولا معادة، وعدّها في أي
/// واحدة منهم بيكذب.
/// </param>
public sealed record DeviceMix(int NewDevices, int Retested, int Unlinked);
