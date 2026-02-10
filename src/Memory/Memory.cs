namespace V6502.Memory;

public class Memory : IMemory
{
    private readonly byte[] _memory;

    public Memory(ushort size)
    {
        _memory = new byte[size];
    }
    
    public byte Read(ushort address)
    {
        return  _memory[address];
    }

    public void Write(ushort address, byte value)
    {
        _memory[address] = value;
    }
}