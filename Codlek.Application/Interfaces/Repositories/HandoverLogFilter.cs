namespace Codlek.Application.Interfaces.Repositories;

/// <summary>فلاتر سجل التسليمات — متظبّطة خلاص.</summary>
public sealed record HandoverLogFilter
{
    /// <summary>من (داخل) — بداية اليوم بتوقيت القاهرة بالـUTC.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>
    /// لـ (<b>خارج</b>) — بداية اليوم اللي بعده.
    ///
    /// <para>🔴 المدى نصف مفتوح: لو كان شامل على بداية اليوم، كل
    /// تسليمات اليوم الأخير كانت تختفي من التقرير.</para>
    /// </summary>
    public DateTime? ToUtc { get; init; }

    public Guid? DestinationId { get; init; }

    /// <summary>
    /// نمط <c>LIKE</c> على اسم المستلم.
    ///
    /// <para>⚠️ ومفيش توحيد عربي عليه — القديم بيهرّب النص وبس.
    /// منقول زي ما هو: التوحيد كان بيلاقي أسماء القديم مابيلاقيهاش،
    /// فنفس البحث بيدّي نتايج مختلفة من الشاشتين.</para>
    /// </summary>
    public string? ReceiverPattern { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = Core.Paging.Paging.DefaultPageSize;
}
