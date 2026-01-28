using System.Runtime.InteropServices;

namespace DiskBench.Win32;

/// <summary>
/// Manages aligned native memory buffers for unbuffered IO.
/// </summary>
internal sealed class AlignedBufferPool : IDisposable
{
    private readonly IntPtr[] _buffers;
    private readonly int _bufferSize;
    private readonly int _alignment;
    private bool _disposed;

    /// <summary>
    /// Gets the number of buffers in the pool.
    /// </summary>
    public int Count => _buffers.Length;

    /// <summary>
    /// Gets the size of each buffer.
    /// </summary>
    public int BufferSize => _bufferSize;

    /// <summary>
    /// Gets the alignment of each buffer.
    /// </summary>
    public int Alignment => _alignment;

    /// <summary>
    /// Creates a new aligned buffer pool.
    /// </summary>
    /// <param name="count">Number of buffers.</param>
    /// <param name="bufferSize">Size of each buffer in bytes.</param>
    /// <param name="alignment">Alignment requirement (must be power of 2).</param>
    public AlignedBufferPool(int count, int bufferSize, int alignment)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);
        if (alignment <= 0 || (alignment & (alignment - 1)) != 0)
            throw new ArgumentException("Alignment must be a power of 2.", nameof(alignment));

        _buffers = new IntPtr[count];
        _bufferSize = bufferSize;
        _alignment = alignment;

        for (int i = 0; i < count; i++)
        {
            _buffers[i] = AllocateAligned(bufferSize, alignment);
        }
    }

    /// <summary>
    /// Gets a buffer by index.
    /// </summary>
    public IntPtr this[int index]
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _buffers[index];
        }
    }

    private static unsafe IntPtr AllocateAligned(int size, int alignment)
    {
        // Use NativeMemory.AlignedAlloc for proper alignment
        void* ptr = NativeMemory.AlignedAlloc((nuint)size, (nuint)alignment);
        if (ptr == null)
        {
            throw new InsufficientMemoryException($"Failed to allocate {size} bytes with {alignment} alignment.");
        }

        // Zero the buffer
        NativeMemory.Clear(ptr, (nuint)size);

        return (IntPtr)ptr;
    }

    /// <summary>
    /// Fills a buffer with a pattern.
    /// </summary>
    public unsafe void FillBuffer(int index, byte pattern)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        NativeMemory.Fill((void*)_buffers[index], (nuint)_bufferSize, pattern);
    }

    /// <summary>
    /// Fills a buffer with random data.
    /// </summary>
    public unsafe void FillRandom(int index, Random random)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var span = new Span<byte>((void*)_buffers[index], _bufferSize);
        random.NextBytes(span);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _buffers.Length; i++)
        {
            if (_buffers[i] != IntPtr.Zero)
            {
                unsafe
                {
                    NativeMemory.AlignedFree((void*)_buffers[i]);
                }
                _buffers[i] = IntPtr.Zero;
            }
        }
    }
}
