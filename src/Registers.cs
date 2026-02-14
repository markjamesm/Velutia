namespace V6502;

public class Registers
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

    public Registers(ushort pc, byte sp, byte a, byte x, byte y, byte p)
    {
        Pc = pc;
        Sp = sp;
        A = a;
        X = x;
        Y = y;
        P = p;
    }

    public Registers()
    {
        Pc = 0;
        Sp = 0;
        A = 0;
        X = 0;
        Y = 0;
        P = 0;
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