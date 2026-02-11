namespace V6502;

public class Memory
{
    private readonly byte[] _memory;

    public Memory(ushort size)
    {
        _memory = new byte[size];
    }

    public Memory(byte[] memory)
    {
        _memory = memory;
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