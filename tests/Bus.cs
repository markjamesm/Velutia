using Velutia.Bus;

namespace Velutia.Cpu.Tests;

public class Bus: IBus, IDisposable
{
    private readonly Memory _memory;
    
    public Bus(Memory memory)
    {
        _memory = memory;
    }

    public byte Read(ushort address)
    {
        return _memory[address];
    }

    public void Write(ushort address, byte value)
    {
        _memory[address] = value;
    }

    public void Dispose()
    {
        _memory.Dispose();
    }
}