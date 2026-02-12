namespace V6502;

public class Memory
{
    private readonly byte[] _ram;

    /// <summary>
    /// Initializes RAM using a given size.
    /// </summary>
    /// <param name="size">The amount of system ram.</param>
    public Memory(ushort size)
    {
        _ram = new byte[size];
    }

    /// <summary>
    /// Loads an existing RAM array into memory.
    /// Useful for testing.
    /// </summary>
    /// <param name="ram">The ram array.</param>
    public Memory(byte[] ram)
    {
        _ram = ram;
    }

    /// <summary>
    /// Reads a value from memory.
    /// </summary>
    /// <param name="address">The memory address.</param>
    /// <returns>the value at the memory location.</returns>
    public byte Read(ushort address)
    {
        return  _ram[address];
    }

    /// <summary>
    /// Writes a value to memory.
    /// </summary>
    /// <param name="address">The memory address.</param>
    /// <param name="value">The value to be written.</param>
    public void Write(ushort address, byte value)
    {
        _ram[address] = value;
    }
}