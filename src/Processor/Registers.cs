namespace Velutia.Processor;

public record Registers
{
    public ushort Pc { get; set; }

    /// <summary>
    /// The 16-bit stack pointer.
    /// </summary>
    public byte Sp { get; set; }

    /// <summary>
    /// The 8-bit accumulator.
    /// </summary>
    public byte A { get; set; }

    // Auxiliary registers
    public byte X { get; set; }
    public byte Y { get; set; }
    
    /// <summary>
    /// Status register (also called P register).
    /// </summary>
    public byte P { get; set; }

    
    /// <summary>
    /// Initialize registers with set values. Used for testing.
    /// </summary>
    /// <param name="pc">Program counter</param>
    /// <param name="sp">Stack pointer</param>
    /// <param name="a">Accumulator</param>
    /// <param name="x">X register</param>
    /// <param name="y">Y register</param>
    /// <param name="p">Status register</param>
    public Registers(ushort pc, byte sp, byte a, byte x, byte y, byte p)
    {
        Pc = pc;
        Sp = sp;
        A = a;
        X = x;
        Y = y;
        P = p;
    }

    /// <summary>
    /// Initializes registers to expected startup values.
    /// </summary>
    public Registers()
    {
        Pc = 0;
        Sp = 0xFD;
        A = 0;
        X = 0;
        Y = 0;
        P = 0b00000100;
    }

    public void SetPFlag(BitOperation operation, StatusRegisterFlags flag)
    {
        if (operation is BitOperation.Set)
        {
            P |= (byte)flag;
        }
        
        else if (operation is BitOperation.Clear)
        {
            P &= (byte)~flag;
        }
    }

    public void SetNzFlags(byte value)
    {
        SetPFlag(value == 0 ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Zero);
        
        SetPFlag((value & (byte)StatusRegisterFlags.Negative) == 0 ? BitOperation.Clear : BitOperation.Set,
            StatusRegisterFlags.Negative);
    }
}