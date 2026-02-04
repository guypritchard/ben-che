using System.Runtime.InteropServices;

namespace DiskBench.Win32;

/// <summary>
/// Represents a single IO operation slot with its OVERLAPPED structure and state.
/// Pinned in memory to allow safe passage to unmanaged code.
/// </summary>
internal sealed class IoSlot : IDisposable
{
    private GCHandle _overlappedHandle;
    private NativeOverlapped _overlapped;
    private bool _disposed;

    /// <summary>
    /// Slot index for identification.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The buffer pointer for this slot.
    /// </summary>
    public IntPtr Buffer { get; }

    /// <summary>
    /// Whether this slot is currently in use (IO pending).
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// Timestamp when the IO was submitted.
    /// </summary>
    public long SubmitTimestamp { get; set; }

    /// <summary>
    /// File offset for the current IO.
    /// </summary>
    public long Offset { get; set; }

    /// <summary>
    /// Size of the current IO.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// Whether the current IO is a write operation.
    /// </summary>
    public bool IsWrite { get; set; }

    /// <summary>
    /// Gets a pointer to the pinned OVERLAPPED structure.
    /// </summary>
    public IntPtr OverlappedPtr => GCHandle.ToIntPtr(_overlappedHandle);

    /// <summary>
    /// Gets a reference to the OVERLAPPED structure.
    /// </summary>
    public ref NativeOverlapped Overlapped => ref _overlapped;

    /// <summary>
    /// Creates a new IO slot.
    /// </summary>
    public IoSlot(int index, IntPtr buffer)
    {
        Index = index;
        Buffer = buffer;

        // Pin the OVERLAPPED structure
        _overlapped = new NativeOverlapped();
        _overlappedHandle = GCHandle.Alloc(_overlapped, GCHandleType.Pinned);
    }

    /// <summary>
    /// Configures the OVERLAPPED for a new IO at the specified offset.
    /// </summary>
    public void Configure(long offset, int size, bool isWrite, long timestamp)
    {
        Offset = offset;
        Size = size;
        IsWrite = isWrite;
        SubmitTimestamp = timestamp;
        IsPending = false;

        // Set offset in OVERLAPPED (split into low and high parts)
        _overlapped.OffsetLow = (int)(offset & 0xFFFFFFFF);
        _overlapped.OffsetHigh = (int)(offset >> 32);
        _overlapped.EventHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_overlappedHandle.IsAllocated)
        {
            _overlappedHandle.Free();
        }
    }
}

/// <summary>
/// Pool of IO slots for managing outstanding operations.
/// </summary>
internal sealed class IoSlotPool : IDisposable
{
    private readonly IoSlot[] _slots;
    private readonly AlignedBufferPool _buffers;
    private bool _disposed;

    /// <summary>
    /// Gets the number of slots in the pool.
    /// </summary>
    public int Count => _slots.Length;

    /// <summary>
    /// Gets the buffer size for each slot.
    /// </summary>
    public int BufferSize => _buffers.BufferSize;

    /// <summary>
    /// Creates a new IO slot pool.
    /// </summary>
    public IoSlotPool(int slotCount, int bufferSize, int alignment)
    {
        _buffers = new AlignedBufferPool(slotCount, bufferSize, alignment);
        _slots = new IoSlot[slotCount];

        for (int i = 0; i < slotCount; i++)
        {
            _slots[i] = new IoSlot(i, _buffers[i])
            {
                Size = bufferSize // Initialize the slot size
            };
        }
    }

    /// <summary>
    /// Gets a slot by index.
    /// </summary>
    public IoSlot this[int index] => _slots[index];

    /// <summary>
    /// Finds a slot by its OVERLAPPED pointer.
    /// </summary>
    public IoSlot? FindByOverlapped(IntPtr overlappedPtr)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].OverlappedPtr == overlappedPtr)
            {
                return _slots[i];
            }
        }
        return null;
    }

    /// <summary>
    /// Finds a slot by index from OVERLAPPED internal data.
    /// </summary>
    public IoSlot GetByIndex(int index)
    {
        return _slots[index];
    }

    /// <summary>
    /// Fills write buffers with pattern data.
    /// </summary>
    public void FillWriteBuffers(byte pattern)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _buffers.FillBuffer(i, pattern);
        }
    }

    /// <summary>
    /// Fills write buffers with random data.
    /// </summary>
    public void FillWriteBuffersRandom(int seed)
    {
        var random = new Random(seed);
        for (int i = 0; i < _slots.Length; i++)
        {
            _buffers.FillRandom(i, random);
        }
    }

    /// <summary>
    /// Resets all slots to not pending.
    /// </summary>
    public void ResetAll()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].IsPending = false;
        }
    }

    /// <summary>
    /// Gets the count of pending operations.
    /// </summary>
    public int GetPendingCount()
    {
        int count = 0;
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].IsPending) count++;
        }
        return count;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Dispose();
        }
        _buffers.Dispose();
    }
}
