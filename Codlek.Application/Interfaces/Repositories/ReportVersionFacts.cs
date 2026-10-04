namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// نسخ البرنامج من الحمولة الخام — <b>نتيجة استعلام، مش
/// جدول</b>.
///
/// <para>⚠️ <b>وليه مش أعمدة.</b> القيمتين دول بيوصلوا جوّه حمولة
/// الفحص. وإضافة عمودين ليهم بس عشان العرض بتضيف هجرة وتكرار
/// بيانات من غير سؤال حقيقي بيتجاوب — لو احتجنا يوم نفلتر أو نجمّع
/// بالنسخة، ساعتها العمود بيبقى له مبرر.</para>
/// </summary>
public sealed record ReportVersionFacts(
    string? ApplicationVersion,
    string? TestDefinitionVersion);
