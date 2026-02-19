using V6502;

namespace Velutia;

public class Bus: IBus
{
    private Memory _memory;
    
    public Bus(Memory memory)
    {
        _memory = memory;
    }

    public byte Read(ushort address)
    {
        return _memory.Read(address);
    }

    public void Write(ushort address, byte value)
    {
        _memory.Write(address, value);
    }
}