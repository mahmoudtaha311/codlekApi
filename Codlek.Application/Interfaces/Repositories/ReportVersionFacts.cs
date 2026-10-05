namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// حقول من الحمولة الخام — النسخ ومين سلّم. <b>نتيجة استعلام، مش
/// جدول</b>.
///
/// <para>⚠️ <b>وليه مش أعمدة.</b> القيم دي بتوصل جوّه حمولة الفحص.
/// وإضافة أعمدة ليها بس عشان العرض بتضيف هجرة وتكرار بيانات من غير
/// سؤال حقيقي بيتجاوب — لو احتجنا يوم نفلتر أو نجمّع بالنسخة، ساعتها
/// العمود بيبقى له مبرر.</para>
///
/// <para>⚠️ <b>و«مين سلّم» في نفس القراية مش قراية تانية</b> — زي
/// القديم بالظبط (<c>ReportRawFields</c>): مسار واحد للحمولة الخام،
/// عشان مايبقاش فيه تطبيقين بيختلفوا.</para>
/// </summary>
public sealed record ReportVersionFacts(
    string? ApplicationVersion,
    string? TestDefinitionVersion,
    string? CompletedByName,
    string? CompletedByCode);
