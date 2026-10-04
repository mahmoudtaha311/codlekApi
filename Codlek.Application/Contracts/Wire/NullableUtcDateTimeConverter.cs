using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codlek.Application.Contracts.Wire;

/// <summary>
/// نفس قاعدة <see cref="UtcDateTimeConverter"/> للتواريخ اللي ممكن
/// تكون <c>null</c>.
///
/// <para>⚠️ <b>وده تأكيد مش إصلاح.</b> <c>System.Text.Json</c> أصلاً
/// بيلفّ محوّل <c>DateTime</c> ويستعمله مع <c>DateTime?</c>
/// (<c>NullableConverterFactory</c>)، فالتواريخ الاختيارية بتخرج
/// بـ<c>Z</c> حتى من غير النوع ده — والقديم بيسجّل محوّل واحد وبيخرج
/// صح. بنسجّله صراحةً عشان الضمانة تبقى في كودنا مش في تفاصيل
/// المكتبة، و<c>WireDateTests</c> بيقيس الاتنين.</para>
///
/// <para>🔴 <b>والحقل الاختياري اللي فارق فعلاً هو
/// <c>highWaterUtc</c></b> — الراكة بتخزّنه <b>كنص خام</b> وبتبعته
/// تاني في <c>?since=</c> من غير ما تفكّه، فأي شكل السيرفر بيطلّعه
/// هو اللي راجع. (التعليق القديم هنا كان بيقول «أوقات الإنهاء
/// والتسليم» — الحقول دي مش موجودة على السطح ده خالص.)</para>
///
/// <para>⚠️ و<c>null</c> بتتكتب <c>null</c> — الراكة بتفرّق بين
/// «مفيش وقت» و«وقت صفر».</para>
/// </summary>
public sealed class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override DateTime? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null
            ? null
            : Inner.Read(ref reader, typeToConvert, options);

    public override void Write(
        Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is { } v) Inner.Write(writer, v, options);
        else writer.WriteNullValue();
    }
}
