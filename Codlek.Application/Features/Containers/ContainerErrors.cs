using Codlek.Application.Abstractions;

namespace Codlek.Application.Features.Containers;

public static class ContainerErrors
{
    public static readonly Error CodeMissing =
        new("container.code_missing", "اكتب رمز الحاوية", 400);

    /// <summary>
    /// ⚠️ <b>«مش صالح» مش زي «فاضي».</b>
    ///
    /// <para>رمز زي <c>---</c> أو <c>///</c> طوله كفاية بس مفيهوش ولا
    /// حرف ولا رقم، فشكله المطبَّع بيطلع فاضي. ولو رجّعنا «اكتب رمز»،
    /// المستخدم بيبص على الخانة ويلاقيها مليانة ومايفهمش.</para>
    /// </summary>
    public static readonly Error CodeInvalid =
        new("container.code_invalid", "رمز الحاوية مش صالح", 400);

    public static Error CodeTaken(string code) =>
        new("container.code_taken", $"الحاوية «{code}» موجودة خلاص", 400);

    public static readonly Error NotFound =
        new("container.not_found", "الحاوية دي مش موجودة.", 404);
}
