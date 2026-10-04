namespace Codlek.Core.Spreadsheets;

/// <summary>
/// صفحة جوّه ملف إكسل — <b>بيانات، مش ملف</b>.
///
/// <para>⚠️ <b>والنوع ده في <c>Core</c> عن قصد.</b> طبقة التطبيق
/// بتبني الصفحات (أعمدة وصفوف) وهي مش عارفة حاجة عن شكل الملف،
/// والكاتب في البنية التحتية بيحوّلها لبايتات. ولو النوع عاش جنب
/// الكاتب، كل معالج تصدير كان هيبقى شايف تفاصيل الصيغة.</para>
///
/// <para>⚠️ <see cref="Rows"/> بتتقرا <b>مرة واحدة وهي بتتكتب</b>.
/// ده مقصود عشان الصفوف تعدّي للملف من غير ما تتلم كلها في الذاكرة
/// — فمتديهاش <c>IEnumerable</c> بيتعاد.</para>
/// </summary>
public sealed record Sheet(string Name, SheetColumn[] Columns, IEnumerable<object?[]> Rows);
