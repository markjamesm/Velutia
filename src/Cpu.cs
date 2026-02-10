namespace V6502;

public class Cpu
{
    //--------------Registers-------------//
    
    // 16-bit program counter
    public ushort Pc {get; private set;}
    
    // 8-bit stack pointer
    public byte Sp {get; private set;}
    
    // The 8-bit accumulator is used all arithmetic and logical operations
    // (except increments and decrements). The contents of the accumulator
    // can be stored and retrieved either from memory or the stack.
    public byte Ac {get; private set;}
    
    // 8-bit auxiliary registers
    public byte X {get; private set;}
    public byte Y {get; private set;}

    // Status register (also called P register)
    /*
       Status Register Flags (bit 7 to bit 0)
       N	Negative
       V	Overflow
       -	ignored
       B	Break
       D	Decimal (use BCD for arithmetics)
       I	Interrupt (IRQ disable)
       Z	Zero
       C	Carry
     */
    public byte P {get; private set;}
    
    // Clock for cycle counting
    public int Clock {get; private set;}

    // Auxiliary
    public Dictionary<ushort, ushort> Ram {get; private set;}

    public Cpu(ushort pc, byte sp, byte ac, byte x, byte y, byte p, Dictionary<ushort, ushort> ram)
    {
        Pc = pc;
        Sp = sp;
        Ac = ac;
        X = x;
        Y = y;
        P = p;
        Clock = 0;
        Ram = ram;
    }

    public void Start()
    {
        Cycle();
    }

    private void Cycle()
    {
        var instruction = FetchInstruction();
        Decode(instruction);
    }

    private ushort FetchInstruction()
    {
        Pc += 1;
        return Pc;
    }

    private void Decode(ushort instruction)
    {
        switch (instruction)
        {
            case 0xEA: Nop();
                break;
        }
    }

    private void Nop()
    {
        Clock += 2;
    }
}