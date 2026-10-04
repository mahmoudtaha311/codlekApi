using System.Text;

namespace Codlek.Core.Text;

/// <summary>
/// رمز حاوية الاستيراد — <b>وشكله المطبَّع</b>.
///
/// <para>🔴 <b>القاعدة دي مشتركة مع الراكة.</b> الفني بيكتب الرمز
/// أوفلاين على الراكة وبيتبعت وقت المزامنة، والسيرفر بيدوّر على نفس
/// الحاوية. فلو الطرفين طبّعوا بشكل مختلف، <b>نفس الشحنة بتبقى
/// حاويتين</b> — واللي ماسك ورقة الاستيراد بيلاقي لاباته متفرّقين على
/// صفّين.</para>
///
/// <para>⚠️ <b>والفني بيكتب الرمز مختلف في كل مرة.</b>
/// <c>SH-2024/01</c> و<c>sh 2024 01</c> و<c>SH202401</c> — دي نفس
/// الشحنة. فالتطبيع بيشيل كل حاجة مش حرف ولا رقم، ويصغّر الحروف.</para>
/// </summary>
public static class ContainerCode
{
    /// <summary>أطول رمز — نفس طول العمود في القاعدة.</summary>
    public const int MaxCodeLength = 40;

    /// <summary>أقصر رمز مقبول.</summary>
    public const int MinCodeLength = 2;

    /// <summary>
    /// الشكل المطبَّع اللي البحث والتفرّد بيشتغلوا عليه.
    /// </summary>
    /// <returns>
    /// نص فاضي لو مفيش ولا حرف ولا رقم في اللي اتكتب — ودي حالة
    /// «الرمز مش صالح»، مش «الرمز فاضي».
    /// </returns>
    public static string Normalize(string? code)
    {
        /*
          ⚠️ **التطبيع العربي الأول، وبعده التصغير.**

          لأن `ArabicText.Normalize` بيوحّد أشكال الألف والهاء/التاء
          المربوطة. ولو صغّرنا الأول، الحروف العربية مالهاش حالة
          فمابيحصلّهاش حاجة — بس الترتيب كده بيفضل مطابق للراكة
          بالحرف، وده اللي المهم.
        */
        string text = ArabicText.Normalize((code ?? "").Trim()).ToLowerInvariant();

        var builder = new StringBuilder(text.Length);

        foreach (char c in text)
            if (char.IsLetterOrDigit(c)) builder.Append(c);

        return builder.ToString();
    }
}
