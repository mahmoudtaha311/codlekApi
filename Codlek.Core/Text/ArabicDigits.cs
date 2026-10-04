namespace Codlek.Core.Text;

/// <summary>
/// أرقام عربية-هندية (<c>٠١٢٣٤٥٦٧٨٩</c>) للرسايل.
///
/// <para>🔴 <b>الحاجة دي اتلقطت من فحص حقيقي على HTTP.</b> رسايل
/// المشروع القديم مكتوبة بالأرقام العربية-الهندية
/// («٥٠٠ لاب» · «١٠ حروف»)، والنسخة الجديدة كانت بتحقن الرقم من
/// ثابت فيطلع «500» و«10». المشروعين هيشتغلوا على نفس القاعدة فترة
/// التحويل، فنفس الطلب لازم يدّي <b>نفس الرسالة بالحرف</b> من
/// الشاشتين.</para>
///
/// <para>⚠️ <b>والحل مش إننا نكتب الرقم في النص بإيدنا.</b> ساعتها
/// الرقم بيبقى مكتوب في مكانين — الثابت والرسالة — وأول تعديل في
/// واحد منهم بيخلّي الرسالة تكذب على المستخدم.</para>
///
/// <para>⚠️ وده للعرض بس: مفيش حاجة بتتقارن ولا بتتخزّن بالشكل
/// ده.</para>
/// </summary>
public static class ArabicDigits
{
    private const string Digits = "٠١٢٣٤٥٦٧٨٩";

    public static string Of(int value)
    {
        string latin = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var text = new System.Text.StringBuilder(latin.Length);

        foreach (char c in latin)
        {
            // ⚠️ السالب والفاصلة بيعدّوا زي ما هم — الأرقام بس اللي
            // بتتحوّل.
            text.Append(c is >= '0' and <= '9' ? Digits[c - '0'] : c);
        }

        return text.ToString();
    }
}
