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
    /// <summary>
    /// القيم اللي الشركات بتحطها بدل السيريال الحقيقي.
    ///
    /// <para>🔴 <b>واللي بيترفض هنا مش مرساة أصلاً</b> — لا بيتخزّن
    /// ولا بيتطابق عليه. ده اللي بيمنع تلتمية لاب HP كلهم بيقولوا
    /// «Default string» من إنهم يبقوا <b>نفس الجهاز</b>.</para>
    /// </summary>
    private static readonly string[] Junk =
    [
        "To Be Filled By O.E.M.", "To Be Filled By OEM", "Default string",
        "System Serial Number", "Not Applicable", "None", "N/A", "NA",
        "0", "00000000", "Unknown", "Not Specified",
        "Chassis Serial Number", "Base Board Serial Number",
        "Product Name", "Manufacturer", "INVALID", "O.E.M.", "OEM",
        "123456789", "Serial", "SerNum0", "Undefined", "Filled By O.E.M.",
        "00000000-0000-0000-0000-000000000000",
        "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF",
    ];

    /// <summary>
    /// القيمة دي حشو مش مرساة؟
    ///
    /// <para>🔴 <b>وده توأم حرفي للي على الراكة.</b> لو الراكة رفضت
    /// قيمة والسيرفر قبلها، نفس اللاب بياخد <b>هويتين مختلفتين</b>
    /// حسب مين اللي طابق — وده أسوأ من إن محدّش يطابق أصلاً.</para>
    /// </summary>
    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        string v = value.Trim();

        foreach (string junk in Junk)
            if (string.Equals(v, junk, StringComparison.OrdinalIgnoreCase)) return true;

        // ⚠️ أقل من ٤ حروف مش مرساة. سيريال حقيقي عمره ما بيبقى «12».
        if (v.Length < 4) return true;

        // ⚠️ حرف واحد متكرر: 0000، FFFFFFFF، xxxxxxxx.
        bool uniform = true;

        for (int i = 1; i < v.Length; i++)
        {
            if (char.ToUpperInvariant(v[i]) != char.ToUpperInvariant(v[0]))
            {
                uniform = false;
                break;
            }
        }

        if (uniform) return true;

        // ⚠️ UUID كله أصفار أو كله F — نفس المعنى بس بالشرطات.
        string stripped = v.Replace("-", "");

        if (stripped.Length >= 8)
        {
            bool allZero = true, allF = true;

            foreach (char c in stripped)
            {
                if (c != '0') allZero = false;
                if (char.ToUpperInvariant(c) != 'F') allF = false;
            }

            if (allZero || allF) return true;
        }

        return false;
    }

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
