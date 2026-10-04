using Codlek.Application.Abstractions;
using FluentValidation;
using MediatR;

namespace Codlek.Application.Behaviors;

/// <summary>
/// بيشغّل المتحقّقات قبل أي أمر.
///
/// <para>🔴 <b>الكلاس ده هو اللي بيخلّي المتحقّقات تعمل حاجة أصلاً.</b>
/// في المشروع المرجعي كان فيه ١٣ متحقّق مكتوبين وشغّالين ع الورق،
/// <b>ومفيش ولا واحد بيتنادى</b> — لأن ماكانش فيه الحتة دي. يعني
/// التحقق كله كان شكل: كل بيانات بتعدّي.</para>
///
/// <para>⚠️ ومفيش متحقّق للأمر؟ يعدّي زي ما هو. أمر مالوش قواعد حاجة
/// عادية، فالغياب مش خطأ.</para>
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);

        /*
          ⚠️ **كل المتحقّقات بتشتغل، مش أول واحد بيفشل.**

          لأن المستخدم اللي سايب تلات خانات فاضية المفروض يشوف
          التلاتة مرة واحدة. لو وقفنا عند أول غلط، بيصلّح واحد
          ويضغط ويلاقي غلط تاني — تلات مرات.
        */
        var failures = (await Task.WhenAll(
                validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count == 0) return await next();

        var errors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

        var error = new ValidationError(errors);

        /*
          🔴 **الفشل بيرجع كـ`Result`، مش استثناء.**

          «البيانات غلط» نتيجة متوقّعة ليها معنى عند المستخدم — مش
          عطل. ولو كانت استثناء، كان `GlobalExceptionHandler` هيحوّلها
          500، والزبون ياخد «في مشكلة في السيرفر» على خانة فاضية.

          ⚠️ والانعكاس تحت هو اللي بيبني `Result` أو `Result<T>` حسب
          نوع الرد. لو الأمر مابيرجّعش `Result`، يبقى مفيش مكان نحطّ
          فيه الفشل — وساعتها الاستثناء هو الصح، وبيبان في الإقلاع
          بدل ما يتسرّب.
        */
        var responseType = typeof(TResponse);

        if (responseType.IsGenericType &&
            responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];

            var failure = typeof(Result)
                .GetMethods()
                .First(m => m.Name == nameof(Result.Failure) && m.IsGenericMethod)
                .MakeGenericMethod(valueType)
                .Invoke(null, [error]);

            return (TResponse)failure!;
        }

        if (responseType == typeof(Result))
            return (TResponse)(object)Result.Failure(error);

        throw new InvalidOperationException(
            $"الأمر {typeof(TRequest).Name} بيرجّع {responseType.Name} مش Result — " +
            "فمفيش مكان نحطّ فيه نتيجة التحقق.");
    }
}
