namespace Codlek.Application.Contracts.Repairs;

/// <summary>
/// جسم نقطة التجاوز الإداري.
///
/// <para>🔴 <b>والسبب مش حقل هنا — هو معامل في سلسلة الاستعلام:</b>
/// <c>POST /repairs/{id}/override/start?reason=…</c>.</para>
///
/// <para>⚠️ ده مش شكل حلو، هو نتيجة إن النقطة القديمة كانت
/// Minimal API والنوع المركّب الواحد خد الجسم. والداش بورد بتبعته
/// كده <b>دلوقتي</b> — فنقله للجسم بيكسر النقطة في صمت: السبب
/// بيوصل فاضي، والرد بيبقى «التجاوز الإداري محتاج سبب مكتوب»
/// وكأن المستخدم نسيه.</para>
/// </summary>
public sealed record StartRepairRequest(Guid TechnicianId);
