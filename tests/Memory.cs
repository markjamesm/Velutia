using System.Buffers;

namespace Velutia.Cpu.Tests;

public class Memory : IDisposable
{
    private const int MemorySize = 64 * 1024;
    
    private readonly byte[] _ram = ArrayPool<byte>.Shared.Rent(MemorySize);

    public byte this[ushort address]
    {
        get => _ram[address];
        set => _ram[address] = value;
    }

    public void Dispose()
    {
        ArrayPool<byte>.Shared.Return(_ram);
    }
}