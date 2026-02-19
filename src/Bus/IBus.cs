namespace V6502;

public interface IBus
{
    public byte Read(ushort address);
    public void Write(ushort address, byte value);
}