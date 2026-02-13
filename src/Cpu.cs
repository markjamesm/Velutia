namespace V6502;

public class Cpu
{
    //--------------Registers-------------//
    
    public ushort Pc { get; private set; }

    // Stack pointer
    public byte S { get; private set; }

    // Accumulator
    public byte A { get; private set; }

    // Auxiliary registers
    public byte X { get; private set; }
    public byte Y { get; private set; }

    // Status register (also called P register)
    // Flags (bit 7 to bit 0)
    /*
       N	Negative
       V	Overflow
       -	ignored
       B	Break
       D	Decimal (use BCD for arithmetics)
       I	Interrupt (IRQ disable)
       Z	Zero
       C	Carry
     */
    public byte P { get; private set; }

    // Clock for cycle counting
    public int Clock { get; private set; }

    public Memory Memory { get; }

    public Cpu(ushort pc, byte s, byte a, byte x, byte y, byte p, Memory memory)
    {
        Pc = pc;
        S = s;
        A = a;
        X = x;
        Y = y;
        P = p;
        Memory = memory;
        Clock = 0;
    }

    public void Start()
    {
        while (true)
        {
            RunInstruction();
        }
    }

    public void RunInstruction()
    {
        var instruction = FetchByte();
        Decode(instruction);
    }

    private byte FetchByte()
    {
        var value = Memory.Read(Pc);
        Pc += 1;

        return value;
    }

    private void Decode(ushort instruction)
    {
        switch (instruction)
        {
            case 0x18:
                Clc();
                break;
            case 0x38:
                Sec();
                break;
            case 0x58:
                Cli();
                break;
            case 0x78:
                Sei();
                break;
            case 0xAD:
                Lda(AddressingMode.Absolute);
                break;
            case 0xB8:
                Clv();
                break;
            case 0xD8:
                Cld();
                break;
            case 0xEA:
                Nop();
                break;
            case 0xF8:
                Sed();
                break;
            case 0x4C:
                Jmp(AddressingMode.Absolute);
                break;
            case 0x6C:
                Jmp(AddressingMode.Indirect);
                break;
        }
    }

    private void Clc()
    {
        P = (byte)(P & ~1);

        Clock += 2;
    }

    private void Cld()
    {
        P = (byte)(P & ~(1 << 3));

        Clock += 2;
    }

    private void Cli()
    {
        P = (byte)(P & ~(1 << 2));

        Clock += 2;
    }

    private void Clv()
    {
        P = (byte)(P & ~(1 << 6));

        Clock += 2;
    }

    private void Jmp(AddressingMode addressingMode)
    {
        // 4C 34 12 --> Jump to $1234
        if (addressingMode == AddressingMode.Absolute)
        {
            // Since PC is incremented when Fetching an instruction
            // [PC] -> PCL, [PC +1] -> PCH
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();

            Pc = (ushort)((ptrHigh << 8) | ptrLow);

            Clock += 3;
        }
        
        // 6C 34 12 --> Jump to the location found at memory $1234 & $1235
        if (addressingMode == AddressingMode.Indirect)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)(ptrHigh << 8 | ptrLow);

            var pcLow = Memory.Read(ptr);
            byte pcHigh;

            // JMP indirect bug: If ptr is at 0xXXFF, the high byte
            // comes from 0xXX00 and not (0xXXFF + 1) as there's no carry.
            if ((ptr & 0xFF) == 0xFF)
            {
                pcHigh = Memory.Read((ushort)(ptr & 0xFF00));
            }
            else
            {
                pcHigh = Memory.Read((ushort)(ptr + 1));
            }
            
            Pc = (ushort)(pcHigh << 8 | pcLow);

            Clock += 5;
        }
    }

    private void Lda(AddressingMode addressingMode)
    {
        if (addressingMode == AddressingMode.Absolute)
        {
            var ptrLow = FetchByte(); //0x4A
            var ptrHigh = FetchByte(); // 0xF2
            var ptr = (ushort)((ptrHigh << 8) | ptrLow); // 0xF24A
            
            A = Memory.Read(ptr);

            // After most instructions that have a value result, this flag will either be set or cleared based on whether or not that value is equal to zero.
            if (A == 0x00)
            {
                P = (byte)(P | 0x1 << 7);
            }

            else
            {
                P = (byte)(P & ~(0x1 << 7));
            }

            P = (byte)(A & 0x80 >> 7);
            
            //     After most instructions that have a value result, this flag will contain bit 7 of that result.
            // BIT will load bit 7 of the addressed value directly into the N flag.
            
            Clock += 4;
        }
    }

    private void Nop()
    {
        Clock += 2;
    }

    private void Sec()
    {
        P = (byte)(P | 0x1);

        Clock += 2;
    }

    private void Sed()
    {
        P = (byte)(P | 0x1 << 3);

        Clock += 2;
    }

    private void Sei()
    {
        P = (byte)(P | 0x1 << 2);
        
        Clock += 2;
    }
}