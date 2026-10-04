namespace Codlek.Application.Contracts.Rack;

/// <summary>
/// رد التسجيل — <b>وفيه المفتاح مرة واحدة</b>.
///
/// <para>🔴 <b>والمفتاح ده مش بيترجّع تاني أبداً.</b> بصمته هي
/// اللي في القاعدة؛ واللي قفل الشاشة قبل ما ياخده محتاج كود تفعيل
/// جديد.</para>
/// </summary>
/// <param name="SyncUrl">
/// 🔴 <b>من إعدادات السيرفر، مش من ترويسة الطلب.</b>
///
/// <para>النسخة الأولى كانت بتبنيه من <c>Request.Host</c> — يعني
/// اللي بيسجّل كان بيحدّد بترويسة <c>Host</c> فين الراكة هترفع
/// شغلها بعد كده، والراكة بتخزّن العنوان وتفضل عليه.</para>
/// </param>
public sealed record RackRegistered(
    Guid RackId,
    string RackCode,
    string RackName,
    Guid TenantId,
    string TenantName,
    string ApiKey,
    string SyncUrl,
    string Message);
