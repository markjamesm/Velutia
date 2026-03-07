using Velutia.Bus;

namespace Velutia.Processor;

public class Cpu
{
    private readonly IBus _bus;
    private readonly Queue<ushort> _irqBuffer = new();
    private readonly Queue<ushort> _nmiBuffer = new();
    private bool _jamFlag;

    private const ushort ResetVector = 0xFFFC;
    private const ushort IrqVector = 0xFFFE;
    private const ushort NmiVector = 0xFFFA;


    public Registers Registers { get; }
    public int Cycles { get; private set; }
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Used for testing.
    /// </summary>
    /// <param name="registers">The register values, set by the SSTs.</param>
    /// <param name="bus">An IBus implementation.</param>
    public Cpu(Registers registers, IBus bus)
    {
        Registers = registers;
        _bus = bus;
        _jamFlag = false;
        IsRunning = true;
        Cycles = 0;
    }

    /// <summary>
    /// Used when implementing the CPU in a larger system.
    /// </summary>
    /// <param name="bus">An IBus implementation.</param>
    public Cpu(IBus bus)
    {
        _bus = bus;
        _jamFlag = false;
        IsRunning = true;
        Registers = new Registers
        {
            Pc = ReadWord(ResetVector)
        };
        Cycles = 0;
    }

    /// <summary>
    /// Resets the CPU to an initial state.
    /// </summary>
    public void Reset()
    {
        Registers.Pc = ReadWord(ResetVector);
        Registers.Sp -= 3;
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);
    }

    public void RunInstruction()
    {
        while (_nmiBuffer.Count > 0)
        {
            var value = _nmiBuffer.Dequeue();

            if (value != NmiVector)
            {
                ProcessNmi(value);
            }

            else
            {
                ProcessNmi();
            }
        }

        while (_irqBuffer.Count > 0 && (Registers.P & (byte)StatusRegisterFlags.Irq) == 0)
        {
            var value = _irqBuffer.Dequeue();
                
            if (value != IrqVector)
            {
                ProcessIrq(value);
            }

            else
            {
                ProcessIrq();
            }
        }
        
        if (!_jamFlag)
        {
            var instruction = FetchByte();
            Decode(instruction);
        }
    }

    private byte FetchByte()
    {
        var value = _bus.Read(Registers.Pc);
        Registers.Pc += 1;

        return value;
    }

    private ushort ReadWord(ushort ptr)
    {
        var lowByte = _bus.Read(ptr);
        var highByte = _bus.Read((ushort)(ptr + 1));

        return (ushort)((highByte << 8) | lowByte);
    }

    public void InitiateIrq(ushort value)
    {
        _irqBuffer.Enqueue(value);
    }

    public void InitiateNmi(ushort value)
    {
        _nmiBuffer.Enqueue(value);
    }

    private void ProcessNmi(ushort value = 0xFFFA)
    {
        PushToStack((byte)((Registers.Pc >> 8) & 0xFF));
        PushToStack((byte)(Registers.Pc & 0xFF));
        PushToStack(Registers.P);
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);
        Registers.Pc = (ushort)(_bus.Read(value) | _bus.Read((ushort)((value + 1) << 8)));
        Cycles += 7;
    }

    private void ProcessIrq(ushort value = 0xFFFE)
    {
        PushToStack((byte)((Registers.Pc >> 8) & 0xFF));
        PushToStack((byte)(Registers.Pc & 0xFF));
        PushToStack((byte)(Registers.P | 0x20));
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);
        Registers.Pc = (ushort)(_bus.Read(value) | _bus.Read((ushort)((value + 1) << 8)));
        Cycles += 7;
    }

    private void PushToStack(byte value)
    {
        _bus.Write((ushort)(0x0100 + Registers.Sp), value);
        Registers.Sp--;
    }

    private byte PopFromStack()
    {
        Registers.Sp++;
        return _bus.Read((ushort)(0x100 + Registers.Sp));
    }


    private static bool IsPageBoundaryCrossed(byte ptrLow, byte ptrHigh, ushort ptr)
    {
        var x = (ushort)(ptrHigh << 8 | ptrLow);
        return (x & 0xFF00) != (ptr & 0xFF00);
    }

    #region GetPtr

    private ushort GetPtr(AddressingMode addressingMode, bool checkForPageBoundaryCross = false)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)((ptrHigh << 8) | ptrLow);

            return ptr;
        }

        if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)((ptrHigh << 8 | ptrLow) + Registers.X);

            if (checkForPageBoundaryCross && IsPageBoundaryCrossed(ptrLow, ptrHigh, ptr))
            {
                Cycles++;
            }

            return ptr;
        }

        if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)((ptrHigh << 8 | ptrLow) + Registers.Y);

            if (checkForPageBoundaryCross && IsPageBoundaryCrossed(ptrLow, ptrHigh, ptr))
            {
                Cycles++;
            }

            return ptr;
        }

        if (addressingMode is AddressingMode.Immediate)
        {
            return 0x00;
        }

        if (addressingMode is AddressingMode.Indirect)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)(ptrHigh << 8 | ptrLow);

            return ptr;
        }

        if (addressingMode is AddressingMode.IndirectX)
        {
            var basePtr = FetchByte();
            var ptrLow = _bus.Read((ushort)((basePtr + Registers.X) % 256));
            var ptrHigh = _bus.Read((ushort)((basePtr + Registers.X + 1) % 256));
            var ptr = (ushort)(ptrHigh << 8 | ptrLow);

            return ptr;
        }

        if (addressingMode is AddressingMode.IndirectY)
        {
            var basePtr = FetchByte();
            var ptrLow = _bus.Read(basePtr);
            var ptrHigh = _bus.Read((ushort)((basePtr + 1) % 256));
            var ptr = (ushort)((ptrHigh << 8 | ptrLow) + Registers.Y);

            if (checkForPageBoundaryCross && IsPageBoundaryCrossed(ptrLow, ptrHigh, ptr))
            {
                Cycles++;
            }

            return ptr;
        }

        if (addressingMode is AddressingMode.Zeropage)
        {
            ushort ptrLow = FetchByte();

            return ptrLow;
        }

        if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = (ushort)((FetchByte() + Registers.X) % 256);

            return ptr;
        }

        if (addressingMode is AddressingMode.ZeropageY)
        {
            var ptr = (ushort)((FetchByte() + Registers.Y) % 256);

            return ptr;
        }

        throw new NotImplementedException();
    }

    #endregion

    #region Decode

    private void Decode(ushort instruction)
    {
        switch (instruction)
        {
            case 0x00:
                Brk();
                break;
            case 0x01:
                Ora(AddressingMode.IndirectX);
                break;
            case 0x02:
                Jam();
                break;
            case 0x03:
                Slo(AddressingMode.IndirectX);
                break;
            case 0x04:
                Nop(AddressingMode.Zeropage);
                break;
            case 0x05:
                Ora(AddressingMode.Zeropage);
                break;
            case 0x06:
                Asl(AddressingMode.Zeropage);
                break;
            case 0x07:
                Slo(AddressingMode.Zeropage);
                break;
            case 0x08:
                Php();
                break;
            case 0x09:
                Ora(AddressingMode.Immediate);
                break;
            case 0x0A:
                Asl(AddressingMode.Accumulator);
                break;
            case 0x0B:
                Anc();
                break;
            case 0x0C:
                Nop(AddressingMode.Absolute);
                break;
            case 0x0D:
                Ora(AddressingMode.Absolute);
                break;
            case 0x0E:
                Asl(AddressingMode.Absolute);
                break;
            case 0x0F:
                Slo(AddressingMode.Absolute);
                break;
            case 0x10:
                Bpl();
                break;
            case 0x11:
                Ora(AddressingMode.IndirectY);
                break;
            case 0x12:
                Jam();
                break;
            case 0x13:
                Slo(AddressingMode.IndirectY);
                break;
            case 0x14:
                Nop(AddressingMode.ZeropageX);
                break;
            case 0x15:
                Ora(AddressingMode.ZeropageX);
                break;
            case 0x16:
                Asl(AddressingMode.ZeropageX);
                break;
            case 0x17:
                Slo(AddressingMode.ZeropageX);
                break;
            case 0x18:
                Clc();
                break;
            case 0x19:
                Ora(AddressingMode.AbsoluteY);
                break;
            case 0x1A:
                Nop(AddressingMode.Implied);
                break;
            case 0x1B:
                Slo(AddressingMode.AbsoluteY);
                break;
            case 0x1C:
                Nop(AddressingMode.AbsoluteX);
                break;
            case 0x1D:
                Ora(AddressingMode.AbsoluteX);
                break;
            case 0x1E:
                Asl(AddressingMode.AbsoluteX);
                break;
            case 0x1F:
                Slo(AddressingMode.AbsoluteX);
                break;
            case 0x20:
                Jsr();
                break;
            case 0x21:
                And(AddressingMode.IndirectX);
                break;
            case 0x22:
                Jam();
                break;
            case 0x23:
                Rla(AddressingMode.IndirectX);
                break;
            case 0x24:
                Bit(AddressingMode.Zeropage);
                break;
            case 0x25:
                And(AddressingMode.Zeropage);
                break;
            case 0x26:
                Rol(AddressingMode.Zeropage);
                break;
            case 0x27:
                Rla(AddressingMode.Zeropage);
                break;
            case 0x28:
                Plp();
                break;
            case 0x29:
                And(AddressingMode.Immediate);
                break;
            case 0x2A:
                Rol(AddressingMode.Accumulator);
                break;
            case 0x2B:
                Anc();
                break;
            case 0x2C:
                Bit(AddressingMode.Absolute);
                break;
            case 0x2D:
                And(AddressingMode.Absolute);
                break;
            case 0x2E:
                Rol(AddressingMode.Absolute);
                break;
            case 0x2F:
                Rla(AddressingMode.Absolute);
                break;
            case 0x30:
                Bmi();
                break;
            case 0x31:
                And(AddressingMode.IndirectY);
                break;
            case 0x32:
                Jam();
                break;
            case 0x33:
                Rla(AddressingMode.IndirectY);
                break;
            case 0x34:
                Nop(AddressingMode.ZeropageX);
                break;
            case 0x35:
                And(AddressingMode.ZeropageX);
                break;
            case 0x36:
                Rol(AddressingMode.ZeropageX);
                break;
            case 0x37:
                Rla(AddressingMode.ZeropageX);
                break;
            case 0x38:
                Sec();
                break;
            case 0x39:
                And(AddressingMode.AbsoluteY);
                break;
            case 0x3A:
                Nop(AddressingMode.Implied);
                break;
            case 0x3B:
                Rla(AddressingMode.AbsoluteY);
                break;
            case 0x3C:
                Nop(AddressingMode.AbsoluteX);
                break;
            case 0x3D:
                And(AddressingMode.AbsoluteX);
                break;
            case 0x3E:
                Rol(AddressingMode.AbsoluteX);
                break;
            case 0x3F:
                Rla(AddressingMode.AbsoluteX);
                break;
            case 0x40:
                Rti();
                break;
            case 0x41:
                Eor(AddressingMode.IndirectX);
                break;
            case 0x42:
                Jam();
                break;
            case 0x43:
                Sre(AddressingMode.IndirectX);
                break;
            case 0x44:
                Nop(AddressingMode.Zeropage);
                break;
            case 0x45:
                Eor(AddressingMode.Zeropage);
                break;
            case 0x46:
                Lsr(AddressingMode.Zeropage);
                break;
            case 0x47:
                Sre(AddressingMode.Zeropage);
                break;
            case 0x48:
                Pha();
                break;
            case 0x49:
                Eor(AddressingMode.Immediate);
                break;
            case 0x4A:
                Lsr(AddressingMode.Accumulator);
                break;
            case 0x4B:
                Alr();
                break;
            case 0x4D:
                Eor(AddressingMode.Absolute);
                break;
            case 0x4E:
                Lsr(AddressingMode.Absolute);
                break;
            case 0x4F:
                Sre(AddressingMode.Absolute);
                break;
            case 0x50:
                Bvc();
                break;
            case 0x51:
                Eor(AddressingMode.IndirectY);
                break;
            case 0x52:
                Jam();
                break;
            case 0x53:
                Sre(AddressingMode.IndirectY);
                break;
            case 0x54:
                Nop(AddressingMode.ZeropageX);
                break;
            case 0x55:
                Eor(AddressingMode.ZeropageX);
                break;
            case 0x56:
                Lsr(AddressingMode.ZeropageX);
                break;
            case 0x57:
                Sre(AddressingMode.ZeropageX);
                break;
            case 0x58:
                Cli();
                break;
            case 0x59:
                Eor(AddressingMode.AbsoluteY);
                break;
            case 0x5A:
                Nop(AddressingMode.Implied);
                break;
            case 0x5B:
                Sre(AddressingMode.AbsoluteY);
                break;
            case 0x5C:
                Nop(AddressingMode.AbsoluteX);
                break;
            case 0x5D:
                Eor(AddressingMode.AbsoluteX);
                break;
            case 0x5E:
                Lsr(AddressingMode.AbsoluteX);
                break;
            case 0x5F:
                Sre(AddressingMode.AbsoluteX);
                break;
            case 0x60:
                Rts();
                break;
            case 0x61:
                Adc(AddressingMode.IndirectX);
                break;
            case 0x62:
                Jam();
                break;
            case 0x63:
                Rra(AddressingMode.IndirectX);
                break;
            case 0x64:
                Nop(AddressingMode.Zeropage);
                break;
            case 0x65:
                Adc(AddressingMode.Zeropage);
                break;
            case 0x66:
                Ror(AddressingMode.Zeropage);
                break;
            case 0x67:
                Rra(AddressingMode.Zeropage);
                break;
            case 0x69:
                Adc(AddressingMode.Immediate);
                break;
            case 0x6D:
                Adc(AddressingMode.Absolute);
                break;
            case 0x6E:
                Ror(AddressingMode.Absolute);
                break;
            case 0x6F:
                Rra(AddressingMode.Absolute);
                break;
            case 0x68:
                Pla();
                break;
            case 0x6A:
                Ror(AddressingMode.Accumulator);
                break;
            case 0x6B:
                Arr();
                break;
            case 0x70:
                Bvs();
                break;
            case 0x71:
                Adc(AddressingMode.IndirectY);
                break;
            case 0x72:
                Jam();
                break;
            case 0x73:
                Rra(AddressingMode.IndirectY);
                break;
            case 0x74:
                Nop(AddressingMode.ZeropageX);
                break;
            case 0x75:
                Adc(AddressingMode.ZeropageX);
                break;
            case 0x76:
                Ror(AddressingMode.ZeropageX);
                break;
            case 0x77:
                Rra(AddressingMode.ZeropageX);
                break;
            case 0x78:
                Sei();
                break;
            case 0x79:
                Adc(AddressingMode.AbsoluteY);
                break;
            case 0x7A:
                Nop(AddressingMode.Implied);
                break;
            case 0x7B:
                Rra(AddressingMode.AbsoluteY);
                break;
            case 0x7C:
                Nop(AddressingMode.AbsoluteX);
                break;
            case 0x7D:
                Adc(AddressingMode.AbsoluteX);
                break;
            case 0x7E:
                Ror(AddressingMode.AbsoluteX);
                break;
            case 0x7F:
                Rra(AddressingMode.AbsoluteX);
                break;
            case 0x80:
                Nop(AddressingMode.Immediate);
                break;
            case 0x81:
                Sta(AddressingMode.IndirectX);
                break;
            case 0x82:
                Nop(AddressingMode.Immediate);
                break;
            case 0x83:
                Sax(AddressingMode.IndirectX);
                break;
            case 0x84:
                Sty(AddressingMode.Zeropage);
                break;
            case 0x85:
                Sta(AddressingMode.Zeropage);
                break;
            case 0x86:
                Stx(AddressingMode.Zeropage);
                break;
            case 0x87:
                Sax(AddressingMode.Zeropage);
                break;
            case 0x88:
                Dey();
                break;
            case 0x89:
                Nop(AddressingMode.Immediate);
                break;
            case 0x8A:
                Txa();
                break;
            case 0x8B:
                Ane();
                break;
            case 0x8C:
                Sty(AddressingMode.Absolute);
                break;
            case 0x8D:
                Sta(AddressingMode.Absolute);
                break;
            case 0x8E:
                Stx(AddressingMode.Absolute);
                break;
            case 0x8F:
                Sax(AddressingMode.Absolute);
                break;
            case 0x90:
                Bcc();
                break;
            case 0x91:
                Sta(AddressingMode.IndirectY);
                break;
            case 0x92:
                Jam();
                break;
            case 0x93:
                Sha();
                break;
            case 0x94:
                Sty(AddressingMode.ZeropageX);
                break;
            case 0x95:
                Sta(AddressingMode.ZeropageX);
                break;
            case 0x96:
                Stx(AddressingMode.ZeropageY);
                break;
            case 0x97:
                Sax(AddressingMode.ZeropageY);
                break;
            case 0x98:
                Tya();
                break;
            case 0x99:
                Sta(AddressingMode.AbsoluteY);
                break;
            case 0x9A:
                Txs();
                break;
            case 0x9B:
                Tas();
                break;
            case 0x9D:
                Sta(AddressingMode.AbsoluteX);
                break;
            case 0x9F:
                Sha();
                break;
            case 0xA0:
                Ldy(AddressingMode.Immediate);
                break;
            case 0xA1:
                Lda(AddressingMode.IndirectX);
                break;
            case 0xA2:
                Ldx(AddressingMode.Immediate);
                break;
            case 0xA3:
                Lax(AddressingMode.IndirectX);
                break;
            case 0xA4:
                Ldy(AddressingMode.Zeropage);
                break;
            case 0xA5:
                Lda(AddressingMode.Zeropage);
                break;
            case 0xA6:
                Ldx(AddressingMode.Zeropage);
                break;
            case 0xA7:
                Lax(AddressingMode.Zeropage);
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
            case 0xAB:
                Lxa();
                break;
            case 0xAC:
                Ldy(AddressingMode.Absolute);
                break;
            case 0xAD:
                Lda(AddressingMode.Absolute);
                break;
            case 0xAE:
                Ldx(AddressingMode.Absolute);
                break;
            case 0xAF:
                Lax(AddressingMode.Absolute);
                break;
            case 0xB0:
                Bcs();
                break;
            case 0xB1:
                Lda(AddressingMode.IndirectY);
                break;
            case 0xB2:
                Jam();
                break;
            case 0xB3:
                Lax(AddressingMode.IndirectY);
                break;
            case 0xB4:
                Ldy(AddressingMode.ZeropageX);
                break;
            case 0xB5:
                Lda(AddressingMode.ZeropageX);
                break;
            case 0xB6:
                Ldx(AddressingMode.ZeropageY);
                break;
            case 0xB7:
                Lax(AddressingMode.ZeropageY);
                break;
            case 0xB8:
                Clv();
                break;
            case 0xB9:
                Lda(AddressingMode.AbsoluteY);
                break;
            case 0xBA:
                Tsx();
                break;
            case 0xBB:
                Las();
                break;
            case 0xBC:
                Ldy(AddressingMode.AbsoluteX);
                break;
            case 0xBD:
                Lda(AddressingMode.AbsoluteX);
                break;
            case 0xBE:
                Ldx(AddressingMode.AbsoluteY);
                break;
            case 0xBF:
                Lax(AddressingMode.AbsoluteY);
                break;
            case 0xC0:
                Cpy(AddressingMode.Immediate);
                break;
            case 0xC1:
                Cmp(AddressingMode.IndirectX);
                break;
            case 0xC2:
                Nop(AddressingMode.Immediate);
                break;
            case 0xC3:
                Dcp(AddressingMode.IndirectX);
                break;
            case 0xC4:
                Cpy(AddressingMode.Zeropage);
                break;
            case 0xC5:
                Cmp(AddressingMode.Zeropage);
                break;
            case 0xC6:
                Dec(AddressingMode.Zeropage);
                break;
            case 0xC7:
                Dcp(AddressingMode.Zeropage);
                break;
            case 0xC8:
                Iny();
                break;
            case 0xC9:
                Cmp(AddressingMode.Immediate);
                break;
            case 0xCA:
                Dex();
                break;
            case 0xCB:
                Sbx();
                break;
            case 0xCC:
                Cpy(AddressingMode.Absolute);
                break;
            case 0xCD:
                Cmp(AddressingMode.Absolute);
                break;
            case 0xCE:
                Dec(AddressingMode.Absolute);
                break;
            case 0xCF:
                Dcp(AddressingMode.Absolute);
                break;
            case 0xD0:
                Bne();
                break;
            case 0xD1:
                Cmp(AddressingMode.IndirectY);
                break;
            case 0xD2:
                Jam();
                break;
            case 0xD3:
                Dcp(AddressingMode.IndirectY);
                break;
            case 0xD4:
                Nop(AddressingMode.ZeropageX);
                break;
            case 0xD5:
                Cmp(AddressingMode.ZeropageX);
                break;
            case 0xD6:
                Dec(AddressingMode.ZeropageX);
                break;
            case 0xD7:
                Dcp(AddressingMode.ZeropageX);
                break;
            case 0xD8:
                Cld();
                break;
            case 0xD9:
                Cmp(AddressingMode.AbsoluteY);
                break;
            case 0xDA:
                Nop(AddressingMode.Implied);
                break;
            case 0xDB:
                Dcp(AddressingMode.AbsoluteY);
                break;
            case 0xDC:
                Nop(AddressingMode.AbsoluteX);
                break;
            case 0xDD:
                Cmp(AddressingMode.AbsoluteX);
                break;
            case 0xDE:
                Dec(AddressingMode.AbsoluteX);
                break;
            case 0xDF:
                Dcp(AddressingMode.AbsoluteX);
                break;
            case 0xE0:
                Cpx(AddressingMode.Immediate);
                break;
            case 0xE1:
                Sbc(AddressingMode.IndirectX);
                break;
            case 0xE2:
                Nop(AddressingMode.Immediate);
                break;
            case 0xE3:
                Isc(AddressingMode.IndirectX);
                break;
            case 0xE4:
                Cpx(AddressingMode.Zeropage);
                break;
            case 0xE5:
                Sbc(AddressingMode.Zeropage);
                break;
            case 0xE6:
                Inc(AddressingMode.Zeropage);
                break;
            case 0xE7:
                Isc(AddressingMode.Zeropage);
                break;
            case 0xE8:
                Inx();
                break;
            case 0xE9:
                Sbc(AddressingMode.Immediate);
                break;
            case 0xEA:
                Nop(AddressingMode.Implied);
                break;
            case 0xEB:
                Sbc(AddressingMode.Immediate);
                break;
            case 0xED:
                Sbc(AddressingMode.Absolute);
                break;
            case 0xEC:
                Cpx(AddressingMode.Absolute);
                break;
            case 0xEE:
                Inc(AddressingMode.Absolute);
                break;
            case 0xEF:
                Isc(AddressingMode.Absolute);
                break;
            case 0xF0:
                Beq();
                break;
            case 0xF1:
                Sbc(AddressingMode.IndirectY);
                break;
            case 0xF2:
                Jam();
                break;
            case 0xF3:
                Isc(AddressingMode.IndirectY);
                break;
            case 0xF4:
                Nop(AddressingMode.ZeropageX);
                break;
            case 0xF5:
                Sbc(AddressingMode.ZeropageX);
                break;
            case 0xF6:
                Inc(AddressingMode.ZeropageX);
                break;
            case 0xF7:
                Isc(AddressingMode.ZeropageX);
                break;
            case 0xF8:
                Sed();
                break;
            case 0xF9:
                Sbc(AddressingMode.AbsoluteY);
                break;
            case 0xFA:
                Nop(AddressingMode.Implied);
                break;
            case 0xFB:
                Isc(AddressingMode.AbsoluteY);
                break;
            case 0xFC:
                Nop(AddressingMode.AbsoluteX);
                break;
            case 0xFD:
                Sbc(AddressingMode.AbsoluteX);
                break;
            case 0xFE:
                Inc(AddressingMode.AbsoluteX);
                break;
            case 0xFF:
                Isc(AddressingMode.AbsoluteX);
                break;
            case 0x4C:
                Jmp(AddressingMode.Absolute);
                break;
            case 0x6C:
                Jmp(AddressingMode.Indirect);
                break;
        }
    }

    #endregion
    
    #region AdcSbcLogic

    private void AdcBinary(byte value)
    {
        var carry = Registers.P & (byte)StatusRegisterFlags.Carry;
        var result = Registers.A + value + carry;
        var sum = (byte)result;

        if (result > 0xFF)
        {
            Registers.P |= (byte)StatusRegisterFlags.Carry;
        }
        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Carry);
        }

        if (((Registers.A ^ sum) & (value ^ sum) & 0x80) != 0)
        {
            Registers.P |= (byte)StatusRegisterFlags.Overflow;
        }
        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Overflow);
        }

        Registers.A = sum;
        Registers.SetNzFlags(Registers.A);
    }

    private void AdcDecimal(byte value)
    {
        var carry = Registers.P & (byte)StatusRegisterFlags.Carry;
        var lowNibble = (Registers.A & 0x0F) + (value & 0x0F) + carry;
        var halfCarry = lowNibble > 0x09;
        var highNibble = (Registers.A >> 4) + (value >> 4) + (halfCarry ? 1 : 0);
        var binaryResult = (byte)((lowNibble & 0x0F) | ((highNibble & 0x0F) << 4));

        if (halfCarry)
        {
            lowNibble += 0x06;
        }

        if (highNibble > 0x09)
        {
            highNibble += 0x06;
        }

        if (highNibble > 0x0F)
        {
            Registers.P |= (byte)StatusRegisterFlags.Carry;
        }

        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Carry);
        }

        if (((Registers.A ^ binaryResult) & (value ^ binaryResult) & 0x80) != 0)
        {
            Registers.P |= (byte)StatusRegisterFlags.Overflow;
        }

        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Overflow);
        }

        Registers.A = (byte)((lowNibble & 0x0F) | ((highNibble & 0x0F) << 4));
        Registers.SetNzFlags(binaryResult);
    }

    private void SbcBinary(byte value)
    {
        var result = 0xFF + Registers.A - value + (Registers.P & (byte)StatusRegisterFlags.Carry);

        if (((Registers.A ^ result) & (~value ^ result) & 0x80) != 0)
        {
            Registers.P |= (byte)StatusRegisterFlags.Overflow;
        }

        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Overflow);
        }

        if (result > 0xFF)
        {
            Registers.P |= (byte)StatusRegisterFlags.Carry;
        }

        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Carry);
        }

        Registers.A = (byte)result;
        Registers.SetNzFlags(Registers.A);
    }

    private void SbcDecimal(byte value)
    {
        var lowNibble = 0xF + (Registers.A & 0xF) - (value & 0xF) + (Registers.P & (byte)StatusRegisterFlags.Carry);
        var halfCarry = lowNibble > 0xF;
        var highNibble = 0xF0 + (Registers.A & 0xF0) - (value & 0xF0) + (halfCarry ? 0x10 : 0);

        if (highNibble > 0xFF)
        {
            Registers.P |= (byte)StatusRegisterFlags.Carry;
        }

        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Carry);
        }

        var binaryResult = (byte)((lowNibble & 0xF) + (highNibble & 0xF0));

        if (!halfCarry)
        {
            lowNibble -= 0x6;
        }

        if ((Registers.P & (byte)StatusRegisterFlags.Carry) == 0)
        {
            highNibble -= 0x60;
        }

        if (((Registers.A ^ binaryResult) & (~value ^ binaryResult) & 0x80) != 0)
        {
            Registers.P |= (byte)StatusRegisterFlags.Overflow;
        }

        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Overflow);
        }

        Registers.A = (byte)((lowNibble & 0xF) + (highNibble & 0xF0));
        Registers.SetNzFlags(binaryResult);
    }

    private bool IsDecimalMode()
    {
        return (Registers.P & (byte)StatusRegisterFlags.Decimal) != 0;
    }
    
    #endregion

    #region instructions

    private void Adc(AddressingMode addressingMode)
    {
        if (addressingMode == AddressingMode.Absolute)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 4;
        }

        else if (addressingMode == AddressingMode.AbsoluteX)
        {
            var value = _bus.Read(GetPtr(addressingMode, true));

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 4;
        }

        else if (addressingMode == AddressingMode.AbsoluteY)
        {
            var value = _bus.Read(GetPtr(addressingMode, true));

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 4;
        }

        else if (addressingMode == AddressingMode.Immediate)
        {
            var value = FetchByte();

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 2;
        }

        else if (addressingMode == AddressingMode.IndirectX)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 6;
        }

        else if (addressingMode == AddressingMode.IndirectY)
        {
            var value = _bus.Read(GetPtr(addressingMode, true));

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 5;
        }

        else if (addressingMode == AddressingMode.Zeropage)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 3;
        }

        else if (addressingMode == AddressingMode.ZeropageX)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                AdcDecimal(value);
            }

            else
            {
                AdcBinary(value);
            }

            Cycles += 4;
        }
    }

    private void Alr()
    {
        var value = FetchByte();

        Registers.A = (byte)(Registers.A & value);
        Registers.SetPFlag((Registers.A & (byte)StatusRegisterFlags.Carry) != 0 ? BitOperation.Set : BitOperation.Clear,
            StatusRegisterFlags.Carry);

        Registers.A >>= 1;
        Registers.SetNzFlags(Registers.A);
        Cycles += 2;
    }

    private void Anc()
    {
        var value = FetchByte();

        Registers.A = (byte)(Registers.A & value);
        var carry = (byte)(Registers.A >> 7);

        Registers.SetPFlag(carry != 0 ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
        Registers.SetNzFlags(Registers.A);
        Cycles += 2;
    }

    private void And(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode, true);
            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode, true);
            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.A = (byte)(Registers.A & FetchByte());
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode, true);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }
    }

    private void Arr()
    {
        var value = FetchByte();
        var andResult = (byte)(Registers.A & value);
        var initialCarry = (Registers.P & (byte)StatusRegisterFlags.Carry) != 0 ? 1 : 0;

        // Binary mode
        if ((Registers.P & (byte)StatusRegisterFlags.Decimal) == 0)
        {
            var rorResult = (byte)((andResult >> 1) | (initialCarry << 7));
            Registers.A = rorResult;

            Registers.SetNzFlags(rorResult);

            // C = bit6 of ROR result
            Registers.SetPFlag((rorResult & 0x40) != 0 ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);

            // V = bit6 XOR bit5 of ROR result
            Registers.SetPFlag(
                ((rorResult & 0x40) != 0) ^ ((rorResult & 0x20) != 0) ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Overflow);
        }

        // Decimal mode
        else
        {
            var highNibble = (byte)(andResult >> 4);
            var lowNibble = (byte)(andResult & 0x0F);

            // N copied from initial carry
            Registers.SetPFlag(initialCarry != 0 ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Negative);

            // ROR result
            var rorResult = (byte)((andResult >> 1) | (initialCarry << 7));

            // Z set from ROR result
            Registers.SetPFlag(rorResult == 0 ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Zero);

            // V = bit6 changed state between AND result and ROR result
            Registers.SetPFlag(((andResult ^ rorResult) & 0x40) != 0 ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Overflow);

            // BCD fixup low nybble
            if (lowNibble + (lowNibble & 1) > 5)
            {
                rorResult = (byte)((rorResult & 0xF0) | ((rorResult + 6) & 0x0F));
            }

            // BCD fixup high nybble + carry
            var highNibbleCarry = highNibble + (highNibble & 1) > 5;
            Registers.SetPFlag(highNibbleCarry ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);

            if (highNibbleCarry)
            {
                rorResult = (byte)((rorResult + 0x60) & 0xFF);
            }

            Registers.A = rorResult;
        }

        Cycles += 2;
    }

    private void Asl(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | 0x0));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | 0x0));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var newCarry = (byte)(Registers.A >> 7);

            Registers.A = (byte)(Registers.A << 1 | 0x0);
            
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | 0x0));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | 0x0));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }
    }

    private void Ane()
    {
        var value = FetchByte();
        Registers.A = (byte)(Registers.A & Registers.X & value);
        Registers.SetNzFlags(Registers.A);
        Cycles += 2;
    }

    private void Bcc()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Carry) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Bcs()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Carry) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Beq()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Zero) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Bit(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var value = _bus.Read(GetPtr(addressingMode));
            var result = (byte)(Registers.A & value);

            const StatusRegisterFlags flags = (StatusRegisterFlags.Negative | StatusRegisterFlags.Overflow);

            Registers.P = (byte)((Registers.P & unchecked((byte)~flags)) | (value & (byte)flags));
            Registers.SetPFlag(result == 0 ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Zero);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var value = _bus.Read(GetPtr(addressingMode));
            var result = (byte)(Registers.A & value);

            const StatusRegisterFlags flags = (StatusRegisterFlags.Negative | StatusRegisterFlags.Overflow);

            Registers.P = (byte)((Registers.P & unchecked((byte)~flags)) | (value & (byte)flags));
            Registers.SetPFlag(result == 0 ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Zero);
            Cycles += 3;
        }
    }

    private void Bmi()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Negative) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Bne()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Zero) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Bpl()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Negative) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Brk()
    {
        var pc = (ushort)(Registers.Pc + 1);
        var pcLow = (byte)(pc & 0xFF);
        var pcHigh = (byte)((pc >> 8) & 0xFF);

        PushToStack(pcHigh);
        PushToStack(pcLow);

        var pushP = (byte)(Registers.P
                           | (byte)StatusRegisterFlags.Break
                           | (byte)StatusRegisterFlags.Ignored);

        PushToStack(pushP);
        Registers.P |= (byte)StatusRegisterFlags.Irq;

        var pLow = _bus.Read(0xFFFE);
        var pHigh = _bus.Read(0xFFFF);
        
        Registers.Pc = (ushort)((pHigh << 8) | pLow);
        Cycles += 7;
    }

    private void Bvc()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Overflow) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Bvs()
    {
        var offset = (sbyte)FetchByte();
        Cycles += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Overflow) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            Cycles += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                Cycles++;
            }
        }
    }

    private void Clc()
    {
        Registers.SetPFlag(BitOperation.Clear, StatusRegisterFlags.Carry);
        Cycles += 2;
    }

    private void Cld()
    {
        Registers.SetPFlag(BitOperation.Clear, StatusRegisterFlags.Decimal);
        Cycles += 2;
    }

    private void Cli()
    {
        Registers.SetPFlag(BitOperation.Clear, StatusRegisterFlags.Irq);
        Cycles += 2;
    }

    private void Clv()
    {
        Registers.SetPFlag(BitOperation.Clear, StatusRegisterFlags.Overflow);
        Cycles += 2;
    }

    private void Cmp(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 4;
        }
    }

    private void Cpx(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.X - value);

            Registers.SetPFlag(Registers.X >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();
            var result = (byte)(Registers.X - value); // Y - Bus

            Registers.SetPFlag(Registers.X >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.X - value);

            Registers.SetPFlag(Registers.X >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 3;
        }
    }

    private void Cpy(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.Y - value);

            Registers.SetPFlag(Registers.Y >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();
            var result = (byte)(Registers.Y - value); // Y - Bus

            Registers.SetPFlag(Registers.Y >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.Y - value);

            Registers.SetPFlag(Registers.Y >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 3;
        }
    }

    private void Dcp(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var decrementedValue = (byte)(value - 1);
            
            _bus.Write(ptr, decrementedValue);

            var result = (byte)(Registers.A - decrementedValue);

            Registers.SetPFlag(Registers.A >= decrementedValue ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var decrementedValue = (byte)(value - 1);
            
            _bus.Write(ptr, decrementedValue);

            var result = (byte)(Registers.A - decrementedValue);

            Registers.SetPFlag(Registers.A >= decrementedValue ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var decrementedValue = (byte)(value - 1);
            
            _bus.Write(ptr, decrementedValue);

            var result = (byte)(Registers.A - decrementedValue);

            Registers.SetPFlag(Registers.A >= decrementedValue ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var decrementedValue = (byte)(value - 1);
            
            _bus.Write(ptr, decrementedValue);

            var result = (byte)(Registers.A - decrementedValue);

            Registers.SetPFlag(Registers.A >= decrementedValue ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var decrementedValue = (byte)(value - 1);
            
            _bus.Write(ptr, decrementedValue);

            var result = (byte)(Registers.A - decrementedValue);

            Registers.SetPFlag(Registers.A >= decrementedValue ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var decrementedValue = (byte)(value - 1);
            _bus.Write(ptr, decrementedValue);

            var result = (byte)(Registers.A - decrementedValue);

            Registers.SetPFlag(Registers.A >= decrementedValue ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var decrementedValue = (byte)(value - 1);
            
            _bus.Write(ptr, decrementedValue);

            var result = (byte)(Registers.A - decrementedValue);

            Registers.SetPFlag(Registers.A >= decrementedValue ? BitOperation.Set : BitOperation.Clear,
                StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);
            Cycles += 6;
        }
    }

    private void Dec(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value - 1));
            Registers.SetNzFlags((byte)(value - 1));
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value - 1));
            Registers.SetNzFlags((byte)(value - 1));
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value - 1));
            Registers.SetNzFlags((byte)(value - 1));
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value - 1));
            Registers.SetNzFlags((byte)(value - 1));
            Cycles += 6;
        }
    }

    private void Dex()
    {
        Registers.X = (byte)(Registers.X - 1);
        Registers.SetNzFlags(Registers.X);
        Cycles += 2;
    }

    private void Dey()
    {
        Registers.Y = (byte)(Registers.Y - 1);
        Registers.SetNzFlags(Registers.Y);
        Cycles += 2;
    }

    private void Eor(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }
    }

    private void Inc(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value + 1));
            Registers.SetNzFlags((byte)(value + 1));
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value + 1));
            Registers.SetNzFlags((byte)(value + 1));
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value + 1));
            Registers.SetNzFlags((byte)(value + 1));
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value + 1));
            Registers.SetNzFlags((byte)(value + 1));
            Cycles += 6;
        }
    }

    private void Inx()
    {
        Registers.X = (byte)(Registers.X + 1);
        Registers.SetNzFlags(Registers.X);
        Cycles += 2;
    }

    private void Iny()
    {
        Registers.Y = (byte)(Registers.Y + 1);
        Registers.SetNzFlags(Registers.Y);
        Cycles += 2;
    }

    private void Isc(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            _bus.Write(ptr, (byte)(value + 1));

            if (IsDecimalMode())
            {
                SbcDecimal((byte)(value + 1));
            }

            else
            {
                SbcBinary((byte)(value + 1));
            }

            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            _bus.Write(ptr, (byte)(value + 1));

            if (IsDecimalMode())
            {
                SbcDecimal((byte)(value + 1));
            }

            else
            {
                SbcBinary((byte)(value + 1));
            }

            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            _bus.Write(ptr, (byte)(value + 1));

            if (IsDecimalMode())
            {
                SbcDecimal((byte)(value + 1));
            }

            else
            {
                SbcBinary((byte)(value + 1));
            }

            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            _bus.Write(ptr, (byte)(value + 1));

            if (IsDecimalMode())
            {
                SbcDecimal((byte)(value + 1));
            }

            else
            {
                SbcBinary((byte)(value + 1));
            }

            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            _bus.Write(ptr, (byte)(value + 1));

            if (IsDecimalMode())
            {
                SbcDecimal((byte)(value + 1));
            }

            else
            {
                SbcBinary((byte)(value + 1));
            }

            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            _bus.Write(ptr, (byte)(value + 1));

            if (IsDecimalMode())
            {
                SbcDecimal((byte)(value + 1));
            }

            else
            {
                SbcBinary((byte)(value + 1));
            }

            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            _bus.Write(ptr, (byte)(value + 1));

            if (IsDecimalMode())
            {
                SbcDecimal((byte)(value + 1));
            }

            else
            {
                SbcBinary((byte)(value + 1));
            }

            Cycles += 6;
        }
    }

    private void Jam()
    {
        _jamFlag = true;
        Cycles += 11;
    }

    private void Jmp(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.Pc = GetPtr(addressingMode);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.Indirect)
        {
            var ptr = GetPtr(addressingMode);
            var pcLow = _bus.Read(ptr);
            byte pcHigh;

            // JMP indirect bug: If ptr is at 0xXXFF, the high byte
            // comes from 0xXX00 and not (0xXXFF + 1) as there's no carry.
            if ((ptr & 0xFF) == 0xFF)
            {
                pcHigh = _bus.Read((ushort)(ptr & 0xFF00));
            }
            else
            {
                pcHigh = _bus.Read((ushort)(ptr + 1));
            }

            Registers.Pc = (ushort)(pcHigh << 8 | pcLow);
            Cycles += 5;
        }
    }

    private void Jsr()
    {
        var pcLow = FetchByte();
        var highByte = (byte)((Registers.Pc >> 8) & 0xFF);
        var lowByte = (byte)(Registers.Pc & 0xFF);
        
        PushToStack(highByte);
        PushToStack(lowByte);

        // SST 20 55 13: The high byte is read from the newly pushed
        // value to the stack, so we need to read the operand high
        // byte after pushing the data to the stack.
        var pcHigh = FetchByte();
        Registers.Pc = (ushort)((pcHigh << 8) | pcLow);
        Cycles += 6;
    }

    private void Las()
    {
        var ptr = GetPtr(AddressingMode.AbsoluteY, true);
        var value = _bus.Read(ptr);
        var result = (byte)(value & Registers.Sp);

        Registers.A = result;
        Registers.X = result;
        Registers.Sp = result;
        Registers.SetNzFlags(result);
        Cycles += 4;
    }

    private void Lax(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            Registers.A = value;
            Registers.X = value;
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);
            
            Registers.A = value;
            Registers.X = value;
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            Registers.A = value;
            Registers.X = value;
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);
            
            Registers.A = value;
            Registers.X = value;
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            Registers.A = value;
            Registers.X = value;
            Registers.SetNzFlags(Registers.A);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            
            Registers.A = value;
            Registers.X = value;
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }
    }

    private void Lda(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode, true));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode, true));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.A = FetchByte();
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode, true));
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }
    }

    private void Ldx(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.X);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode, true));
            Registers.SetNzFlags(Registers.X);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.X = FetchByte();
            Registers.SetNzFlags(Registers.X);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.X);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageY)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.X);
            Cycles += 4;
        }
    }

    private void Ldy(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.Y = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.Y);
            Cycles += 4;
        }

        if (addressingMode is AddressingMode.AbsoluteX)
        {
            Registers.Y = _bus.Read(GetPtr(addressingMode, true));
            Registers.SetNzFlags(Registers.Y);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.Y = FetchByte();
            Registers.SetNzFlags(Registers.Y);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            Registers.Y = _bus.Read(ptr);
            Registers.SetNzFlags(Registers.Y);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            Registers.Y = _bus.Read(ptr);
            Registers.SetNzFlags(Registers.Y);
            Cycles += 4;
        }
    }

    private void Lsr(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 & ~0x80));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 & ~0x80));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var newCarry = (byte)(Registers.A & 0x1);

            Registers.A = (byte)(Registers.A >> 1 & ~0x80);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 & ~0x80));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 & ~0x80));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }
    }

    private void Lxa()
    {
        var value = FetchByte();

        Registers.A = value;
        Registers.X = value;
        Registers.SetNzFlags(value);
        Cycles += 2;
    }

    private void Nop(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            GetPtr(addressingMode);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            GetPtr(addressingMode, true);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Implied)
        {
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            FetchByte();
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            GetPtr(addressingMode);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            GetPtr(addressingMode);
            Cycles += 4;
        }
    }

    private void Ora(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode, true);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);
            Cycles += 4;
        }
    }

    private void Pha()
    {
        PushToStack(Registers.A);
        Cycles += 3;
    }

    private void Php()
    {
        var processorStatus = (byte)(Registers.P | (1 << 4));
        PushToStack(processorStatus);
        Cycles += 3;
    }

    private void Pla()
    {
        Registers.A = PopFromStack();
        Registers.SetNzFlags(Registers.A);
        Cycles += 4;
    }

    private void Plp()
    {
        var processorStatus = PopFromStack();

        const StatusRegisterFlags statusRegisterFlags =
            StatusRegisterFlags.Carry | StatusRegisterFlags.Zero | StatusRegisterFlags.Irq |
            StatusRegisterFlags.Decimal | StatusRegisterFlags.Overflow |
            StatusRegisterFlags.Negative;

        Registers.P =
            (byte)((Registers.P & unchecked((byte)~statusRegisterFlags)) |
                   (processorStatus & (byte)statusRegisterFlags));

        Cycles += 4;
    }

    private void Rla(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);
            var rol = (byte)(value << 1 | oldCarry);

            _bus.Write(ptr, rol);

            Registers.A &= rol;
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);
            var rol = (byte)(value << 1 | oldCarry);

            _bus.Write(ptr, rol);

            Registers.A &= rol;
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);
            var rol = (byte)(value << 1 | oldCarry);

            _bus.Write(ptr, rol);

            Registers.A &= rol;
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);
            var rol = (byte)(value << 1 | oldCarry);

            _bus.Write(ptr, rol);

            Registers.A &= rol;
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);
            var rol = (byte)(value << 1 | oldCarry);

            _bus.Write(ptr, rol);

            Registers.A &= rol;
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);
            var rol = (byte)(value << 1 | oldCarry);

            _bus.Write(ptr, rol);

            Registers.A &= rol;
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);
            var rol = (byte)(value << 1 | oldCarry);

            _bus.Write(ptr, rol);

            Registers.A &= rol;
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }
    }

    private void Rol(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | oldCarry));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | oldCarry));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(Registers.A >> 7);

            Registers.A = (byte)(Registers.A << 1 | oldCarry);

            // Clear carry before setting it.
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | oldCarry));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | oldCarry));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }
    }

    private void Ror(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 | (oldCarry << 7)));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 | (oldCarry << 7)));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(Registers.A & 0x1);

            Registers.A = (byte)(Registers.A >> 1 | (oldCarry << 7));

            // Clear carry before setting it.
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 | (oldCarry << 7)));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 | (oldCarry << 7)));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));
            Cycles += 6;
        }
    }

    private void Rra(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);
            var rorOper = (byte)(value >> 1 | (oldCarry << 7));

            _bus.Write(ptr, rorOper);

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            if (IsDecimalMode())
            {
                AdcDecimal(rorOper);
            }

            else
            {
                AdcBinary(rorOper);
            }

            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);
            var rorOper = (byte)(value >> 1 | (oldCarry << 7));

            _bus.Write(ptr, rorOper);

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            if (IsDecimalMode())
            {
                AdcDecimal(rorOper);
            }

            else
            {
                AdcBinary(rorOper);
            }

            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);
            var rorOper = (byte)(value >> 1 | (oldCarry << 7));

            _bus.Write(ptr, rorOper);

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            if (IsDecimalMode())
            {
                AdcDecimal(rorOper);
            }

            else
            {
                AdcBinary(rorOper);
            }

            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);
            var rorOper = (byte)(value >> 1 | (oldCarry << 7));

            _bus.Write(ptr, rorOper);

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            if (IsDecimalMode())
            {
                AdcDecimal(rorOper);
            }

            else
            {
                AdcBinary(rorOper);
            }

            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);
            var rorOper = (byte)(value >> 1 | (oldCarry << 7));

            _bus.Write(ptr, rorOper);

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            if (IsDecimalMode())
            {
                AdcDecimal(rorOper);
            }

            else
            {
                AdcBinary(rorOper);
            }

            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);
            var rorOper = (byte)(value >> 1 | (oldCarry << 7));

            _bus.Write(ptr, rorOper);

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            if (IsDecimalMode())
            {
                AdcDecimal(rorOper);
            }

            else
            {
                AdcBinary(rorOper);
            }

            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(value & 0x1);
            var rorOper = (byte)(value >> 1 | (oldCarry << 7));

            _bus.Write(ptr, rorOper);

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            if (IsDecimalMode())
            {
                AdcDecimal(rorOper);
            }

            else
            {
                AdcBinary(rorOper);
            }

            Cycles += 6;
        }
    }

    private void Rti()
    {
        var pFlags = PopFromStack();
        
        const StatusRegisterFlags statusRegisterFlags =
            StatusRegisterFlags.Carry | StatusRegisterFlags.Zero | StatusRegisterFlags.Irq |
            StatusRegisterFlags.Decimal | StatusRegisterFlags.Overflow |
            StatusRegisterFlags.Negative;

        Registers.P =
            (byte)((Registers.P & unchecked((byte)~statusRegisterFlags)) |
                   (pFlags & (byte)statusRegisterFlags));

        var pcLow = PopFromStack();
        var pcHigh = PopFromStack();
        
        Registers.Pc = (ushort)((pcHigh << 8) | pcLow);
        Cycles += 6;
    }

    private void Rts()
    {
        var pcLow = PopFromStack();
        var pcHigh = PopFromStack();

        Registers.Pc = (ushort)((pcHigh << 8) | pcLow);
        Registers.Pc = (ushort)(Registers.Pc + 1);
        Cycles += 6;
    }

    private void Sax(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(AddressingMode.Absolute);
            _bus.Write(ptr, (byte)(Registers.A & Registers.X));
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(AddressingMode.IndirectX);
            _bus.Write(ptr, (byte)(Registers.A & Registers.X));
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(AddressingMode.Zeropage);
            _bus.Write(ptr, (byte)(Registers.A & Registers.X));
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageY)
        {
            var ptr = GetPtr(AddressingMode.ZeropageY);
            _bus.Write(ptr, (byte)(Registers.A & Registers.X));
            Cycles += 4;
        }
    }

    private void Sbc(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var value = _bus.Read(GetPtr(addressingMode, true));

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var value = _bus.Read(GetPtr(addressingMode, true));

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var value = _bus.Read(GetPtr(addressingMode, true));

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var value = _bus.Read(GetPtr(addressingMode));

            if (IsDecimalMode())
            {
                SbcDecimal(value);
            }

            else
            {
                SbcBinary(value);
            }

            Cycles += 4;
        }
    }

    private void Sbx()
    {
        var value = FetchByte();
        var temp = (byte)(Registers.A & Registers.X);
        var result = (byte)(temp - value);

        Registers.X = result;

        Registers.SetPFlag(temp >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
        Registers.SetNzFlags(result);
        Cycles += 2;
    }

    private void Sec()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Carry);
        Cycles += 2;
    }

    private void Sed()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Decimal);
        Cycles += 2;
    }

    private void Sei()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);
        Cycles += 2;
    }

    private void Sha()
    {
        System.Diagnostics.Debug.WriteLine("SHA instruction detected.");
    }

    public void Slo(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);
            var asl = (byte)(value << 1 | 0x0);

            _bus.Write(ptr, asl);

            Registers.A = (byte)(Registers.A | asl);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);
            var asl = (byte)(value << 1 | 0x0);

            _bus.Write(ptr, asl);

            Registers.A = (byte)(Registers.A | asl);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);
            var asl = (byte)(value << 1 | 0x0);

            _bus.Write(ptr, asl);

            Registers.A = (byte)(Registers.A | asl);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);
            var asl = (byte)(value << 1 | 0x0);

            _bus.Write(ptr, asl);

            Registers.A = (byte)(Registers.A | asl);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);
            var asl = (byte)(value << 1 | 0x0);

            _bus.Write(ptr, asl);

            Registers.A = (byte)(Registers.A | asl);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);
            var asl = (byte)(value << 1 | 0x0);

            _bus.Write(ptr, asl);

            Registers.A = (byte)(Registers.A | asl);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value >> 7);
            var asl = (byte)(value << 1 | 0x0);

            _bus.Write(ptr, asl);

            Registers.A = (byte)(Registers.A | asl);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }
    }

    private void Sre(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);
            var lsr = (byte)(value >> 1 & ~0x80);

            _bus.Write(ptr, lsr);

            Registers.A = (byte)(Registers.A ^ lsr);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);
            var lsr = (byte)(value >> 1 & ~0x80);

            _bus.Write(ptr, lsr);

            Registers.A = (byte)(Registers.A ^ lsr);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);
            var lsr = (byte)(value >> 1 & ~0x80);

            _bus.Write(ptr, lsr);

            Registers.A = (byte)(Registers.A ^ lsr);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 7;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);
            var lsr = (byte)(value >> 1 & ~0x80);

            _bus.Write(ptr, lsr);

            Registers.A = (byte)(Registers.A ^ lsr);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);
            var lsr = (byte)(value >> 1 & ~0x80);

            _bus.Write(ptr, lsr);

            Registers.A = (byte)(Registers.A ^ lsr);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 8;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);
            var lsr = (byte)(value >> 1 & ~0x80);

            _bus.Write(ptr, lsr);

            Registers.A = (byte)(Registers.A ^ lsr);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var newCarry = (byte)(value & 0x1);
            var lsr = (byte)(value >> 1 & ~0x80);

            _bus.Write(ptr, lsr);

            Registers.A = (byte)(Registers.A ^ lsr);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);
            Cycles += 6;
        }
    }

    private void Sta(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);
            Cycles += 5;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);
            Cycles += 6;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);
            Cycles += 4;
        }
    }

    private void Stx(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            _bus.Write(GetPtr(addressingMode), Registers.X);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            _bus.Write(GetPtr(addressingMode), Registers.X);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageY)
        {
            _bus.Write(GetPtr(addressingMode), Registers.X);
            Cycles += 4;
        }
    }

    private void Sty(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            _bus.Write(GetPtr(addressingMode), Registers.Y);
            Cycles += 4;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            _bus.Write(GetPtr(addressingMode), Registers.Y);
            Cycles += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.Y);
            Cycles += 4;
        }
    }

    private void Tas()
    {
        Registers.Sp = (byte)(Registers.A & Registers.X);

        var ptr = GetPtr(AddressingMode.AbsoluteY);
        var highByte = (byte)((ptr >> 8) + 1);

        // AND SP with high byte + 1, store in memory
        var effectiveAddress = (ushort)(ptr + Registers.Y);
        
        _bus.Write(effectiveAddress, (byte)(Registers.Sp & highByte));
        Cycles += 5;
    }

    private void Tax()
    {
        Registers.X = Registers.A;
        Registers.SetNzFlags(Registers.X);
        Cycles += 2;
    }

    private void Tay()
    {
        Registers.Y = Registers.A;
        Registers.SetNzFlags(Registers.Y);
        Cycles += 2;
    }

    private void Tsx()
    {
        Registers.X = Registers.Sp;
        Registers.SetNzFlags(Registers.X);
        Cycles += 2;
    }

    private void Txa()
    {
        Registers.A = Registers.X;
        Registers.SetNzFlags(Registers.A);
        Cycles += 2;
    }

    private void Txs()
    {
        Registers.Sp = Registers.X;
        Cycles += 2;
    }

    private void Tya()
    {
        Registers.A = Registers.Y;
        Registers.SetNzFlags(Registers.A);
        Cycles += 2;
    }

    #endregion
}