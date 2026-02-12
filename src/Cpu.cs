namespace V6502;

public class Cpu
{
    //--------------Registers-------------//

    // 16-bit program counter
    public ushort Pc { get; private set; }

    // 8-bit stack pointer
    public byte S { get; private set; }

    // The 8-bit accumulator is used all arithmetic and logical operations
    // (except increments and decrements). The contents of the accumulator
    // can be stored and retrieved either from memory or the stack.
    public byte A { get; private set; }

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
            case 0x18:
                Clc();
                break;
            case 0xD8:
                Cld();
                break;
            case 0xEA:
                Nop();
                break;
            case 0x4C:
            case 0x6C:
                Jmp(instruction);
                break;
        }
    }

    // Clear carry
    private void Clc()
    {
        P = (byte)(P & ~1);
        
        Clock += 2;
    }
    
    // Clear decimal
    private void Cld()
    {
        P = (byte)(P & ~(1 << 3));

        Clock += 2;
    }
    
    private void Jmp(ushort instruction)
    {
        // Absolute jump
        // 4C 34 12 --> Jump to $1234
        if (instruction == 0x4C)
        {
            // [PC + 1] → PCL, [PC + 2] → PCH
            // Since PC is incremented when Fetching an instruction, [PC] -> PCL, [PC +1] -> PCH
            var pcl = Memory.Read(Pc);
            var pch = Memory.Read((ushort)(Pc + 1));

            Pc = (ushort)((pch << 8) | pcl);

            Clock += 3;
        }

        // Indirect jump
        // 6C 34 12 --> Jump to the location found at memory $1234 & $1235
        if (instruction == 0x6C)
        {
            // #1 Read opcode, increment PC, decode opcode
            // #2 Read operand (lower) at PC, increment PC
            // #3 Read operand (upper) at PC, increment PC
            // #4 Read effective PCL at (upper, lower)
            // #5 Read effective PCH at (upper, lower + 1)
            // #6 Assign PC = (PCH, PCL)
            
            //AN INDIRECT JUMP MUST NEVER USE A
            // VECTOR BEGINNING ON THE LAST BYTE
            // OF A PAGE
            // if address $3000 contains $40, $30FF contains $80, and $3100 contains $50,
            // the result of JMP ($30FF) will be a transfer of control to $4080
            // rather than $5080 as you intended i.e. the 6502 took the low byte
            // of the address from $30FF and the high byte from $3000. 
            
            var lowOrderByte = Memory.Read(Pc);
            var highOrderByte = Memory.Read((ushort)(Pc + 1));
            
            var pcl = Memory.Read((ushort)((highOrderByte << 8) | lowOrderByte));
            var pch = Memory.Read((ushort)(highOrderByte << 8 | (lowOrderByte + 1)));
            
            Pc = (ushort)(pch << 8 | pcl);

            Clock += 5;
        }
    }

    private void Nop()
    {
        Clock += 2;
    }
}