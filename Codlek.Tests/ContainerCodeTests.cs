using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// تطبيع رمز الحاوية — <b>وهي قاعدة مشتركة مع الراكة</b>.
///
/// <para>🔴 <b>الفني بيكتب الرمز أوفلاين على الراكة</b> وبيتبعت وقت
/// المزامنة، والسيرفر بيدوّر على نفس الحاوية بالشكل المطبَّع. فلو
/// الطرفين طبّعوا بشكل مختلف، <b>نفس الشحنة بتبقى حاويتين</b> —
/// واللي ماسك ورقة الاستيراد بيلاقي لاباته متفرّقين على صفّين.</para>
/// </summary>
public class ContainerCodeTests
{
    /// <summary>
    /// 🔴 كل الأشكال اللي الفني بيكتبها لنفس الشحنة بتطلع مفتاح واحد.
    /// </summary>
    [Theory]
    [InlineData("SH-2024/01", "sh202401")]
    [InlineData("sh 2024 01", "sh202401")]
    [InlineData("SH202401", "sh202401")]
    [InlineData("  sh-2024-01  ", "sh202401")]
    [InlineData("SH.2024.01", "sh202401")]
    public void Every_way_the_technician_writes_it_gives_one_key(string written, string key) =>
        Assert.Equal(key, ContainerCode.Normalize(written));

    /// <summary>
    /// ⚠️ والحروف العربية بتتطبّع كمان — <c>حاوية</c> و<c>حاويه</c>
    /// نفس الشحنة.
    /// </summary>
    [Fact]
    public void Arabic_forms_normalise_to_the_same_key() =>
        Assert.Equal(
            ContainerCode.Normalize("شحنة ٢٠٢٤"),
            ContainerCode.Normalize("شحنه ٢٠٢٤"));

    /// <summary>
    /// 🔴 <b>رمز مفيهوش ولا حرف ولا رقم بيطلع فاضي — ودي حالة «مش
    /// صالح» مش «فاضي».</b>
    ///
    /// <para>الفرق بيبان للمستخدم: لو رجّعنا «اكتب رمز الحاوية» وهو
    /// كاتب <c>---</c>، بيبص على الخانة ويلاقيها مليانة ومايفهمش.</para>
    /// </summary>
    [Theory]
    [InlineData("---")]
    [InlineData("///")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("")]
    public void A_code_with_no_letters_or_digits_normalises_to_empty(string junk) =>
        Assert.Equal("", ContainerCode.Normalize(junk));

    [Fact]
    public void Null_is_treated_as_empty() =>
        Assert.Equal("", ContainerCode.Normalize(null));

    /// <summary>
    /// ⚠️ ورمزين مختلفين فعلاً بيفضلوا مختلفين — <b>التطبيع مش
    /// بيدمج</b>.
    /// </summary>
    [Fact]
    public void Two_genuinely_different_codes_stay_different() =>
        Assert.NotEqual(
            ContainerCode.Normalize("SH-2024/01"),
            ContainerCode.Normalize("SH-2024/02"));

    /// <summary>
    /// ⚠️ والأطوال ثوابت مربوطة بأعمدة القاعدة — مش أرقام في الكود.
    /// </summary>
    [Fact]
    public void The_length_limits_match_the_database_column()
    {
        Assert.Equal(40, ContainerCode.MaxCodeLength);
        Assert.Equal(2, ContainerCode.MinCodeLength);
    }
}
