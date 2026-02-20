namespace Velutia.Bus;

public interface IBus
{
    public byte Read(ushort address);
    public void Write(ushort address, byte value);
}