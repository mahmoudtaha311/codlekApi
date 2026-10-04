namespace Codlek.Core.Text;

/// <summary>
/// توحيد قيم مراسي الهوية (سيريالات · UUID · أرقام بيوس).
///
/// <para>🔴 <b>ودي <u>مش</u> <see cref="ArabicText.Normalize"/>.</b>
/// القيم دي لاتينية وتقنية: <c>ABC-123</c> و<c>abc 123</c> نفس
/// السيريال، لكن التوحيد العربي كان بيلمس حروف مالهاش وجود هنا
/// ومابيكبّرش الحروف — فنفس السيريال بحالة أحرف مختلفة كان بيبان
/// جهازين.</para>
///
/// <para>⚠️ <b>والقاعدة بسيطة بقصد:</b> شيل المساحات من الطرفين،
/// لمّ كل سلسلة مساحات لمساحة واحدة، وكبّر الحروف. ومفيش شيل
/// للشُرَط ولا للنقط — <c>ABC-123</c> و<c>ABC123</c> سيريالين
/// مختلفين فعلاً على عتاد حقيقي.</para>
/// </summary>
public static class IdentityValues
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";

        var text = new System.Text.StringBuilder(value.Length);
        bool lastWasSpace = false;

        foreach (char c in value.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                // ⚠️ سلسلة مساحات بتبقى مساحة واحدة — الفني بيلزق
                // القيمة من برنامج تاني وبتيجي فيها تابات.
                if (!lastWasSpace)
                {
                    text.Append(' ');
                    lastWasSpace = true;
                }

                continue;
            }

            lastWasSpace = false;
            text.Append(char.ToUpperInvariant(c));
        }

        return text.ToString().Trim();
    }
}
