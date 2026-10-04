using System.Text.Json;
using System.Text.Json.Serialization;

namespace Codlek.Api.Racks;

/// <summary>
/// نفس قاعدة <see cref="UtcDateTimeConverter"/> للتواريخ اللي ممكن
/// تكون <c>null</c>.
///
/// <para>🔴 <b>ومحوّل منفصل لازم مش رفاهية.</b>
/// <c>JsonConverter&lt;DateTime&gt;</c> مابينطبقش على
/// <c>DateTime?</c> — فمن غير النوع ده، كل تاريخ اختياري على سطح
/// الراكة بيتكتب من غير <c>Z</c>. والحقول الاختيارية دي بالظبط هي
/// أوقات الإنهاء والتسليم.</para>
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
