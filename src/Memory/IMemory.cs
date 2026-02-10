namespace V6502.Memory;

public interface IMemory
{
    public byte Read(ushort address);
    public void Write(ushort address, byte value);
}