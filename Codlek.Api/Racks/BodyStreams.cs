namespace Codlek.Api.Racks;

/// <summary>
/// بيوقف القراءة لو الجسم عدّى الحد.
///
/// <para>🔴 <b>فحص <c>Content-Length</c> لوحده مش كفاية.</b> الترويسة
/// ممكن تكون غلط، وممكن الطلب يبقى <c>chunked</c> من غير طول أصلاً —
/// وساعتها الحد اللي على الترويسة مابيشتغلش خالص. ده الحزام التاني
/// اللي بيشتغل على <b>البايتات الحقيقية</b>.</para>
///
/// <para>⚠️ <b>وده مهم لأن الخدمة على استضافة ذاكرتها نص جيجا.</b>
/// طلب كبير واحد بيقدر يوقّع الموقع لكل الرواكة مش لواحدة.</para>
/// </summary>
public sealed class LimitedStream(Stream inner, long limit) : Stream
{
    private long _read;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _read;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int n = inner.Read(buffer, offset, count);
        Count(n);
        return n;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        int n = await inner.ReadAsync(buffer, cancellationToken);
        Count(n);
        return n;
    }

    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    private void Count(int n)
    {
        _read += n;

        if (_read > limit)
            throw new BadHttpRequestException(
                "الحمولة أكبر من المسموح", StatusCodes.Status413PayloadTooLarge);
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}

/// <summary>
/// بيرجّع بايتات اتقرت خلاص قبل باقي الـ<c>Stream</c>.
///
/// <para>⚠️ <b>محتاجينه عشان نعرف شكل الـJSON</b> (مصفوفة مباشرة ولا
/// غلاف <c>{ Version, Reports }</c>) من غير ما نقرا الجسم كله في
/// الذاكرة. وجسم الطلب <b>مش</b> بيقبل الرجوع للخلف، فالـ<c>Seek</c>
/// مش حل.</para>
/// </summary>
public sealed class PrefixedStream(byte[] prefix, Stream rest) : Stream
{
    private int _offset;

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_offset < prefix.Length)
        {
            int take = Math.Min(count, prefix.Length - _offset);

            Array.Copy(prefix, _offset, buffer, offset, take);
            _offset += take;

            return take;
        }

        return rest.Read(buffer, offset, count);
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_offset < prefix.Length)
        {
            int take = Math.Min(buffer.Length, prefix.Length - _offset);

            prefix.AsMemory(_offset, take).CopyTo(buffer);
            _offset += take;

            return take;
        }

        return await rest.ReadAsync(buffer, cancellationToken);
    }

    public override Task<int> ReadAsync(
        byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
}
