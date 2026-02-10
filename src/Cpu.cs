using V6502.Memory;

namespace V6502;

public class Cpu
{
    //--------------Registers-------------//

    // 16-bit program counter
    public ushort Pc { get; private set; }

    // 8-bit stack pointer
    public byte Sp { get; private set; }

    // The 8-bit accumulator is used all arithmetic and logical operations
    // (except increments and decrements). The contents of the accumulator
    // can be stored and retrieved either from memory or the stack.
    public byte Ac { get; private set; }

    // 8-bit auxiliary registers
    public byte X { get; private set; }
    public byte Y { get; private set; }

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
    public byte P { get; private set; }

    // Clock for cycle counting
    public int Clock { get; private set; }

    // create an interface Memory with read() and write(), and then two classes that implement it,
    // MemorySst, which implements it with a dictionary, and Memory, which implements
    // it with an array of bytes.

    // Address: Value
    public IMemory Memory { get; private set; }

    public Cpu(ushort pc, byte sp, byte ac, byte x, byte y, byte p, IMemory memory)
    {
        Pc = pc;
        Sp = sp;
        Ac = ac;
        X = x;
        Y = y;
        P = p;
        Clock = 0;
        Memory = memory;
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
        var instruction = FetchInstruction();
        Decode(instruction);
    }

    private ushort FetchInstruction()
    {
        var instruction = Memory.Read(Pc);
        Pc += 1;
        return instruction;
    }

    private void Decode(ushort instruction)
    {
        switch (instruction)
        {
            case 0xEA:
                Nop();
                break;
            case 0x4C:
            case 0x6C:
                Jmp(instruction);
                break;
        }
    }

    private void Nop()
    {
        Clock += 2;
    }

    private void Jmp(ushort instruction)
    {
        // Absolute jump
        // 4C 34 12 --> Jump to $1234
        if (instruction == 0x4C)
        {
            // [PC + 1] → PCL, [PC + 2] → PCH
            // We increment Pc when Fetching an instruction, so [PC] -> PCL, [PC +1] -> PCH
            var pcl = Memory.Read(Pc);
            var pch = Memory.Read((ushort)(Pc + 1));

            Pc = (ushort)((pch << 8) | pcl);

            Clock += 3;
        }

        // Indirect jump
        // 6C 34 12 --> Jump to the location found at memory $1234 and $1235
        if (instruction == 0x6C)
        {
            // #1 Read opcode, increment PC, decode opcode
            // #2 Read operand (lower) at PC, increment PC
            // #3 Read operand (upper) at PC, increment PC
            // #4 Read effective PCL at (upper, lower)
            // #5 Read effective PCH at (upper, lower + 1)
            //    Assign PC = (PCH, PCL)
            var lowOrderByte = Memory.Read(Pc);
            var highOrderByte = Memory.Read((ushort)(Pc + 1));

            var address = (ushort)((highOrderByte << 8) | lowOrderByte);
            
            var pcl = Memory.Read(address);
            var pch = Memory.Read((ushort)(address + 1));
            
            Pc = (ushort)(pch << 8 | pcl);

            Clock += 5;
        }
    }
}