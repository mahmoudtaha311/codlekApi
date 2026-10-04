using Codlek.Core.Text;

namespace Codlek.Tests;

/// <summary>
/// قواعد النص النقية — <b>وهي مشتركة مع الراكة</b>.
///
/// <para>🔴 <b>تطبيع العربي مش تفصيلة شكلية.</b> اتجرّب فعلاً إن
/// <c>Arabic_CI_AI</c> على SQL Server <b>مش</b> بيخلّي «أحمد» تساوي
/// «احمد» — فالتطبيع بيحصل في الكود، والبحث كله مبني عليه. لو اتغيّر،
/// كل اسم متخزّن في القاعدة بيبقى مش قابل للبحث بالشكل اللي الناس
/// بتكتبه.</para>
///
/// <para>⚠️ وفيه نسخة من نفس القاعدة على الراكة. الاتنين لازم يدّوا
/// نفس الناتج بالحرف، وإلا نفس اللاب بياخد بصمة مختلفة على الطرفين.</para>
/// </summary>
public class TextRuleTests
{
    /// <summary>
    /// 🔴 الفحص الذاتي اللي المشروع القديم بيشغّله وقت الإقلاع.
    ///
    /// <para>هناك هو بيرمي ويمنع السيرفر يقوم — وده كان الاختيار
    /// الصح وقتها لأن مكانش فيه مكان تاني يتحط فيه. هنا بقى فحص
    /// عادي: بيقع في ثانية بدل ما يوقّف إقلاع، والرسالة بتقول
    /// الحالة الفاشلة بالاسم.</para>
    /// </summary>
    [Fact]
    public void The_arabic_normalisation_self_test_passes()
    {
        var failures = ArabicText.SelfTest();

        Assert.True(failures.Count == 0,
            "تطبيع العربي فشل في: " + string.Join(" · ", failures));
    }

    /// <summary>
    /// ⚠️ والشاهد إن الفحص الذاتي بيفحص حاجة فعلاً.
    ///
    /// <para>من غير السطر ده، لو <c>SelfTest</c> رجّعت قايمة فاضية
    /// لأي سبب، الفحص اللي فوق بيعدّي وهو مش بيقيس حاجة.</para>
    /// </summary>
    [Fact]
    public void Normalisation_actually_folds_the_forms_people_type()
    {
        Assert.Equal(ArabicText.Normalize("أحمد"), ArabicText.Normalize("احمد"));
        Assert.Equal(ArabicText.Normalize("إبراهيم"), ArabicText.Normalize("ابراهيم"));
        Assert.Equal(ArabicText.Normalize("يحيى"), ArabicText.Normalize("يحيي"));

        // ⚠️ ومش بيطبّق على اللاتيني: «HP» مالهاش شكل تاني.
        Assert.NotEqual(ArabicText.Normalize("HP"), ArabicText.Normalize("Dell"));
    }

    /// <summary>
    /// ⚠️ اسم الدخول بيتطبّع بنفس القاعدة.
    ///
    /// <para>ده اللي بيمنع حسابين بنفس الاسم بأشكال مختلفة.</para>
    /// </summary>
    [Fact]
    public void Login_names_normalise_the_same_way()
    {
        Assert.Equal(LoginName.Normalize("Ahmed"), LoginName.Normalize("ahmed"));
        Assert.Equal(LoginName.Normalize(" ahmed "), LoginName.Normalize("ahmed"));
    }
}
