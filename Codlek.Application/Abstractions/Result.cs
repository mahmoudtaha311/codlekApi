namespace Codlek.Application.Abstractions;

/// <summary>
/// نتيجة عملية — <b>نجحت ولا فشلت ومعاها السبب</b>.
///
/// <para>⚠️ <b>الاستثناء للأعطال مش للرفض.</b> «الباسورد غلط» مش عطل
/// — هي نتيجة متوقّعة ليها معنى عند المستخدم. الاستثناء بيتساب
/// للحاجات اللي محدش يقدر يعملها (القاعدة واقعة، ملف مش موجود).</para>
/// </summary>
public class Result
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; } = default!;

    public Result(bool isSuccess, Error error)
    {
        // 🔴 نجاح معاه سبب فشل، أو فشل من غير سبب — الاتنين حالة
        // مستحيلة، والمنشئ بيمنعها بدل ما تتسرّب للمنادي.
        if ((isSuccess && error != Error.None) || (!isSuccess && error == Error.None))
            throw new InvalidOperationException("نتيجة متناقضة: النجاح والسبب مش متفقين.");

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

/// <summary>نتيجة ومعاها قيمة.</summary>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    public Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error) =>
        _value = value;

    /// <summary>
    /// ⚠️ بترمي لو النتيجة فشل — عن قصد. قراية قيمة من نتيجة فاشلة
    /// غلط برمجي، والسكوت عنه بيطلّع <c>null</c> في مكان بعيد.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("النتيجة فاشلة — مفيش قيمة.");
}
