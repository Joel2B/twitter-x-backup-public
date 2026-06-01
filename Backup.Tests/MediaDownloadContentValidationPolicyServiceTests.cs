using System.Net;
using Backup.Application.Media;

namespace Backup.Tests;

public class MediaDownloadContentValidationPolicyServiceTests
{
    private readonly IMediaDownloadContentValidationPolicyService _sut =
        new MediaDownloadContentValidationPolicyService();

    [Fact]
    public void EnsureSuccessStatusCode_Throws_WhenNotOk()
    {
        SystemException ex = Assert.Throws<SystemException>(
            () => _sut.EnsureSuccessStatusCode(HttpStatusCode.NotFound)
        );
        Assert.Equal("NotFound", ex.Message);
    }

    [Fact]
    public void EnsureReadable_Throws_WhenStreamNotReadable()
    {
        using Stream stream = new UnreadableStream();
        SystemException ex = Assert.Throws<SystemException>(() => _sut.EnsureReadable(stream));
        Assert.Equal("content is empty.", ex.Message);
    }

    [Fact]
    public void EnsureNotEmpty_Throws_WhenLengthZero()
    {
        SystemException ex = Assert.Throws<SystemException>(() => _sut.EnsureNotEmpty(0));
        Assert.Equal("content is empty.", ex.Message);
    }

    [Fact]
    public void EnsureComplete_Throws_WhenLengthMismatch()
    {
        SystemException ex = Assert.Throws<SystemException>(() => _sut.EnsureComplete(100, 50));
        Assert.Equal("incomplete download", ex.Message);
    }

    [Fact]
    public void EnsureComplete_DoesNothing_WhenExpectedLengthMissing()
    {
        _sut.EnsureComplete(null, 50);
    }

    private sealed class UnreadableStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => 0;
        public override long Position
        {
            get => 0;
            set { }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) => 0;

        public override long Seek(long offset, SeekOrigin origin) => 0;

        public override void SetLength(long value) { }

        public override void Write(byte[] buffer, int offset, int count) { }
    }
}
