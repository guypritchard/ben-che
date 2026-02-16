using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DiskBench.Win32;

/// <summary>
/// Represents a single IO operation slot with its OVERLAPPED structure and state.
/// Pinned in memory to allow safe passage to unmanaged code.
/// </summary>
internal sealed class IoSlot : IDisposable
{
    private IntPtr _overlappedPtr;
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
    public IntPtr OverlappedPtr => _overlappedPtr;

    /// <summary>
    /// Gets a reference to the OVERLAPPED structure.
    /// </summary>
    public unsafe ref NativeOverlapped Overlapped => ref Unsafe.AsRef<NativeOverlapped>((void*)_overlappedPtr);

    /// <summary>
    /// Creates a new IO slot.
    /// </summary>
    public IoSlot(int index, IntPtr buffer)
    {
        Index = index;
        Buffer = buffer;

        // Allocate unmanaged OVERLAPPED memory to keep it stable during async IO
        _overlappedPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlapped>());
        Marshal.StructureToPtr(new NativeOverlapped(), _overlappedPtr, false);
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
        ref var overlapped = ref Overlapped;
        overlapped.OffsetLow = (int)(offset & 0xFFFFFFFF);
        overlapped.OffsetHigh = (int)(offset >> 32);
        overlapped.EventHandle = IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_overlappedPtr != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_overlappedPtr);
            _overlappedPtr = IntPtr.Zero;
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
