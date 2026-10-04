using Codlek.Core.Enums;

namespace Codlek.Application.Interfaces.Repositories;

/// <summary>
/// حِمل راكة — <b>بالحالة كـenum</b>.
///
/// <para>⚠️ النص العربي بيتحسب في الـHandler: ترجمة جوّه الإسقاط
/// بتترجم عادي وبتعدّي فحوص الوحدة، وبترمي على قاعدة حقيقية.</para>
/// </summary>
public sealed record RackLoadFacts(
    Guid Id,
    string Code,
    string Name,
    RackStatus Status,
    int Reports,
    DateTime? LastSeenAtUtc);
