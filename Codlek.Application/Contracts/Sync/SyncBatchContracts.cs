namespace Codlek.Application.Contracts.Sync;

// =====================================================================
//  دفعة المزامنة — /api/v2/sync/batch
//
//  🔴 **أخطر عقد في النظام.** الراكة بتقفل صف طابورها (وبتمسحه) لو
//  الرد ٢xx ومالقتش صفّها في `results` — فرد شكله نجاح بيمسح شغل فني
//  في صمت. الأسماء والترتيب هنا **منقولين بالحرف** من القديم، والرد
//  بيتسلسل بـ`RackWire.Wire` بس.
// =====================================================================

/// <summary>صف واحد في دفعة مزامنة.</summary>
public sealed class SyncItemRequest
{
    /// <summary>معرّف الصف في طابور الراكة — <b>ده اللي الرد بيتقفل بيه</b>.</summary>
    public Guid OutboxId { get; set; }

    /// <summary><c>device</c> · <c>report</c> · <c>workitem</c> · <c>workflow</c>.</summary>
    public string EntityType { get; set; } = "";

    public string EntityId { get; set; } = "";
    public long SequenceNumber { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public int PayloadVersion { get; set; }

    /// <summary>SHA-256 للنص القانوني. السيرفر بيحسبها تاني ويقارن.</summary>
    public string PayloadHash { get; set; } = "";

    /// <summary>
    /// الحمولة <b>كنص</b> مش ككائن متداخل.
    ///
    /// <para>⚠️ التحقق من الهاش لازم يتعمل على نفس البايتات اللي
    /// الراكة حسبت عليها. لو اتبعتت ككائن، السيرفر كان هيعيد تسلسلها
    /// ويطلع نص تاني والمقارنة تبقى بلا معنى.</para>
    /// </summary>
    public string Payload { get; set; } = "";
}

/// <summary>دفعة مزامنة.</summary>
public sealed class SyncBatchRequest
{
    /// <summary>معرّف الدفعة — <b>إعادة إرسالها بترجّع نفس الرد بالظبط</b>.</summary>
    public Guid BatchId { get; set; }

    public string RackCode { get; set; } = "";
    public List<SyncItemRequest>? Items { get; set; } = [];
}

/// <summary>سبب رفض صف.</summary>
public sealed class SyncItemError
{
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";

    /// <summary>
    /// الإعادة ممكن تنفع؟ <b>صريحة عن قصد</b> — الراكة بتصدّقها بدل ما
    /// تستنتج من كود HTTP.
    /// </summary>
    public bool Retryable { get; set; }

    /// <summary>
    /// الحالة المبدئية لكل صف — <b>رفض قابل للإعادة</b>. شوف
    /// <see cref="SyncItemResult.Status"/>.
    /// </summary>
    public static SyncItemError NotProcessed() => new()
    {
        Code = SyncBatchCodes.NotProcessed,
        Message = "الصف ماتحسمش على السيرفر — هيتعاد.",
        Retryable = true,
    };
}

/// <summary>
/// نتيجة صف واحد.
///
/// <para>🔴 <b>الحالة مجموعة مقفولة من تلاتة، و<c>Rejected</c> هي
/// الوحيدة اللي الراكة مابتمسحش عليها.</b> أي قيمة تانية — حتى
/// قيمة غلط — بتتقري «اتقفل».</para>
/// </summary>
public sealed class SyncItemResult
{
    public Guid OutboxId { get; set; }
    public string EntityId { get; set; } = "";

    /// <summary>
    /// <see cref="SyncItemStatus"/>.
    ///
    /// <para>🔴 <b>والافتراضي <c>Rejected</c> — مش <c>Applied</c>
    /// زي القديم.</b> الافتراضي هو اللي بيطلع لو أي مسار نسي يحط
    /// الحالة: في القديم ده كان «اتطبّق» يعني الراكة تمسح الصف وهو
    /// ماوصلش. هنا بيبقى رفض <b>قابل للإعادة</b> — أسوأ حاجة إن
    /// الصف يتعاد.</para>
    /// </summary>
    public string Status { get; set; } = SyncItemStatus.Rejected;

    /// <summary>هاش الحمولة طابق حساب السيرفر؟</summary>
    public bool HashMatch { get; set; }

    public SyncItemError? Error { get; set; } = SyncItemError.NotProcessed();
}

public sealed class SyncBatchSummary
{
    public int Applied { get; set; }
    public int Unchanged { get; set; }
    public int Rejected { get; set; }

    /// <summary>
    /// الفحوص وحدها من <see cref="Applied"/>.
    ///
    /// <para>⚠️ <see cref="Applied"/> بيعدّ <b>كل</b> نوع — أجهزة
    /// وأوامر وحركات. عدّاد «الفحوص اللي وصلت» على المحطة لازم يعدّ
    /// الفحوص بس، وإلا الرقم اللي المدير بيقيّم بيه الإنتاجية بيتضخّم.</para>
    /// </summary>
    public int ReportsApplied { get; set; }
}

public sealed class SyncBatchResponse
{
    public Guid BatchId { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime ServerTimeUtc { get; set; }

    public List<SyncItemResult> Results { get; set; } = [];
    public SyncBatchSummary Summary { get; set; } = new();
}

/// <summary>
/// حالات الصف — <b>نصوص على السلك</b>.
///
/// <para>⚠️ الراكة بتقارنها من غير حساسية لحالة الأحرف، بس السيرفر
/// بيكتبها بالشكل ده بالظبط.</para>
/// </summary>
public static class SyncItemStatus
{
    public const string Applied = "Applied";
    public const string Unchanged = "Unchanged";
    public const string Rejected = "Rejected";
}

/// <summary>أكواد رفض الصف على مستوى الدفعة نفسها.</summary>
public static class SyncBatchCodes
{
    /// <summary>
    /// ⚠️ <b>الحالة المبدئية لكل صف</b> — لو ظهرت في رد، يبقى فيه
    /// مسار نسي يحسم الصف. قابلة للإعادة عن قصد.
    /// </summary>
    public const string NotProcessed = "NotProcessed";

    public const string InvalidPayload = "InvalidPayload";
    public const string MissingFields = "MissingFields";
    public const string UnsupportedEntity = "UnsupportedEntity";
    public const string UnknownEntityType = "UnknownEntityType";
    public const string DeviceNotSynced = "DeviceNotSynced";
    public const string DeviceRejected = "DeviceRejected";
}
