namespace V6502;

public class Cpu
{
    // Clock for cycle counting
    public int Clock { get; private set; }

    public Memory Memory { get; }

    public Registers Registers { get; private set; }

    public Cpu(Registers registers, Memory memory)
    {
        Registers = registers;
        Memory = memory;
        Clock = 0;
    }

    public Cpu(Memory memory)
    {
        Memory = memory;
        Registers = new Registers();
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
        var value = Memory.Read(Registers.Pc);
        Registers.Pc += 1;

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
            case 0x48:
                Pha();
                break;
            case 0x58:
                Cli();
                break;
            case 0x78:
                Sei();
                break;
            case 0x88:
                Dey();
                break;
            case 0x8A:
                Txa();
                break;
            case 0x98:
                Tya();
                break;
            case 0x9A:
                Txs();
                break;
            case 0xA5:
                Lda(AddressingMode.ZeroPage);
                break;
            case 0xA8:
                Tay();
                break;
            case 0xA9:
                Lda(AddressingMode.Immediate);
                break;
            case 0xAA:
                Tax();
                break;
            case 0xAD:
                Lda(AddressingMode.Absolute);
                break;
            case 0xB8:
                Clv();
                break;
            case 0xBA:
                Tsx();
                break;
            case 0xC8:
                Iny();
                break;
            case 0xCA:
                Dex();
                break;
            case 0xD8:
                Cld();
                break;
            case 0xE8:
                Inx();
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

    private ushort GetPtr(AddressingMode addressingMode)
    {
        if (addressingMode == AddressingMode.Absolute)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)((ptrHigh << 8) | ptrLow);

            return ptr;
        }

        else if (addressingMode == AddressingMode.Indirect)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)(ptrHigh << 8 | ptrLow);

            return ptr;
        }

        else if (addressingMode == AddressingMode.ZeroPage)
        {
            var ptrLow = Memory.Read(Registers.Pc);
            var ptr = (ushort)(ptrLow & ~0xFF00);

            return ptr;
        }

        else
        {
            throw new NotImplementedException();
        }
    }


    private void Clc()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Carry);

        Clock += 2;
    }

    private void Cld()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Decimal);

        Clock += 2;
    }

    private void Cli()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);

        Clock += 2;
    }

    private void Clv()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Overflow);

        Clock += 2;
    }

    private void Dex()
    {
        Registers.X = (byte)(Registers.X - 1);
        Registers.SetNzFlags(Registers.X);

        Clock += 2;
    }

    private void Dey()
    {
        Registers.Y = (byte)(Registers.Y - 1);
        Registers.SetNzFlags(Registers.Y);

        Clock += 2;
    }

    private void Inx()
    {
        Registers.X = (byte)(Registers.X + 1);
        Registers.SetNzFlags(Registers.X);

        Clock += 2;
    }

    private void Iny()
    {
        Registers.Y = (byte)(Registers.Y + 1);
        Registers.SetNzFlags(Registers.Y);

        Clock += 2;
    }

    private void Jmp(AddressingMode addressingMode)
    {
        if (addressingMode == AddressingMode.Absolute)
        {
            Registers.Pc = GetPtr(AddressingMode.Absolute);

            Clock += 3;
        }

        if (addressingMode == AddressingMode.Indirect)
        {
            var ptr = GetPtr(AddressingMode.Indirect);
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

            Registers.Pc = (ushort)(pcHigh << 8 | pcLow);

            Clock += 5;
        }
    }

    private void Lda(AddressingMode addressingMode)
    {
        if (addressingMode == AddressingMode.Absolute)
        {
            var ptr = GetPtr(AddressingMode.Absolute);

            Registers.A = Memory.Read(ptr);
            Registers.SetNzFlags(Registers.A);

            Clock += 4;
        }

        else if (addressingMode == AddressingMode.Immediate)
        {
            Registers.A = FetchByte();
            Registers.SetNzFlags(Registers.A);

            Clock += 2;
        }

        else if (addressingMode == AddressingMode.ZeroPage)
        {
            var ptr = GetPtr(AddressingMode.ZeroPage);

            Registers.A = Memory.Read(ptr);
            Registers.SetNzFlags(Registers.A);

            FetchByte();

            Clock += 3;
        }
    }

    private void Nop()
    {
        Clock += 2;
    }

    private void Pha()
    {
        Memory.Write((ushort)(0x0100 + Registers.Sp), Registers.A);
        Registers.Sp -= 1;
        
        Clock += 3;
    }

    private void Sec()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Carry);

        Clock += 2;
    }

    private void Sed()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Decimal);

        Clock += 2;
    }

    private void Sei()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);

        Clock += 2;
    }

    private void Tax()
    {
        Registers.X = Registers.A;
        Registers.SetNzFlags(Registers.X);

        Clock += 2;
    }

    private void Tay()
    {
        Registers.Y = Registers.A;
        Registers.SetNzFlags(Registers.Y);

        Clock += 2;
    }

    private void Tsx()
    {
        Registers.X = Registers.Sp;
        Registers.SetNzFlags(Registers.X);

        Clock += 2;
    }

    private void Txa()
    {
        Registers.A = Registers.X;
        Registers.SetNzFlags(Registers.A);

        Clock += 2;
    }

    private void Txs()
    {
        Registers.Sp = Registers.X;

        Clock += 2;
    }

private void Tya()
    {
        Registers.A = Registers.Y;
        Registers.SetNzFlags(Registers.A);

        Clock += 2;
    }
}