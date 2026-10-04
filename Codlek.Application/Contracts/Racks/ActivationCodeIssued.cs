namespace Codlek.Application.Contracts.Racks;

/// <summary>
/// الكود الكامل — <b>بيتعرض مرة واحدة بس</b>.
///
/// <para>⚠️ ومفيش نقطة تانية بترجّعه. المدير اللي قفل الصفحة قبل ما
/// ياخده لازم يعمل كود جديد — وده أرخص من كود بيقدر يتقرا تاني.</para>
/// </summary>
public sealed record ActivationCodeIssued(
    string Code,
    string IntendedName,
    DateTime ExpiresAtUtc);
