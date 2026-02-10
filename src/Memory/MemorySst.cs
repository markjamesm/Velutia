namespace V6502.Memory;

// This class implements system memory as a dictionary
// to populate only the necessary values from the single
// step tests.
public class MemorySst : IMemory
{
    private Dictionary<ushort, byte> _memory;

    public MemorySst(Dictionary<ushort, byte> memory)
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
}