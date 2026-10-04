namespace Codlek.Core.Text;

/// <summary>ليه الماركة دي اتربطت باللاب ده.</summary>
public enum BrandMatch
{
    /// <summary>مطابقة كاملة على الاسم أو على اسم بديل.</summary>
    Exact,

    /// <summary>
    /// أول كلمة طابقت — <c>Dell Inc.</c> ← <c>Dell</c>.
    ///
    /// <para>⚠️ بترجع كسبب منفصل عن المطابقة الكاملة عن قصد:
    /// لو طلعت غلط، اللي بيبص لازم يعرف إنها اتحلّت بأول كلمة
    /// مش بمطابقة.</para>
    /// </summary>
    FirstWord,

    /// <summary>ماركة مش في القايمة.</summary>
    Unknown
}
