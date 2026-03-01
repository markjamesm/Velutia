using Velutia.Bus;

namespace Velutia.Processor;

public class Cpu
{
    private int _clock;

    private readonly IBus _bus;

    public Registers Registers { get; }

    public Cpu(Registers registers, IBus bus)
    {
        Registers = registers;
        _bus = bus;
        _clock = 0;
    }

    public Cpu(IBus bus)
    {
        _bus = bus;
        Registers = new Registers();
        _clock = 0;
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
        var value = _bus.Read(Registers.Pc);
        Registers.Pc += 1;

        return value;
    }

    #region GetPtr

    private ushort GetPtr(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)((ptrHigh << 8) | ptrLow);

            return ptr;
        }

        // Add a cycle for write instructions or for page
        // wrapping on read instructions (Abs X, Abs Y)
        if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)((ptrHigh << 8 | ptrLow) + Registers.X);

            return ptr;
        }

        if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptrLow = FetchByte();
            var ptrHigh = FetchByte();
            var ptr = (ushort)((ptrHigh << 8 | ptrLow) + Registers.Y);

            return ptr;
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
            case 0x05:
                Ora(AddressingMode.Zeropage);
                break;
            case 0x06:
                Asl(AddressingMode.Zeropage);
                break;
            case 0x08:
                Php();
                break;
            case 0x09:
                Ora(AddressingMode.Immediate);
                break;
            case 0x10:
                Bpl();
                break;
            case 0x11:
                Ora(AddressingMode.IndirectY);
                break;
            case 0x16:
                Asl(AddressingMode.ZeropageX);
                break;
            case 0x19:
                Ora(AddressingMode.AbsoluteY);
                break;
            case 0x0A:
                Asl(AddressingMode.Accumulator);
                break;
            case 0x0D:
                Ora(AddressingMode.Absolute);
                break;
            case 0x0E:
                Asl(AddressingMode.Absolute);
                break;
            case 0x15:
                Ora(AddressingMode.ZeropageX);
                break;
            case 0x18:
                Clc();
                break;
            case 0x1D:
                Ora(AddressingMode.AbsoluteX);
                break;
            case 0x1E:
                Asl(AddressingMode.AbsoluteX);
                break;
            case 0x20:
                Jsr();
                break;
            case 0x21:
                And(AddressingMode.IndirectX);
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
            case 0x28:
                Plp();
                break;
            case 0x29:
                And(AddressingMode.Immediate);
                break;
            case 0x2A:
                Rol(AddressingMode.Accumulator);
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
            case 0x30:
                Bmi();
                break;
            case 0x31:
                And(AddressingMode.IndirectY);
                break;
            case 0x35:
                And(AddressingMode.ZeropageX);
                break;
            case 0x36:
                Rol(AddressingMode.ZeropageX);
                break;
            case 0x38:
                Sec();
                break;
            case 0x39:
                And(AddressingMode.AbsoluteY);
                break;
            case 0x3D:
                And(AddressingMode.AbsoluteX);
                break;
            case 0x3E:
                Rol(AddressingMode.AbsoluteX);
                break;
            case 0x40:
                Rti();
                break;
            case 0x41:
                Eor(AddressingMode.IndirectX);
                break;
            case 0x45:
                Eor(AddressingMode.Zeropage);
                break;
            case 0x46:
                Lsr(AddressingMode.Zeropage);
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
            case 0x4D:
                Eor(AddressingMode.Absolute);
                break;
            case 0x4E:
                Lsr(AddressingMode.Absolute);
                break;
            case 0x50:
                Bvc();
                break;
            case 0x51:
                Eor(AddressingMode.IndirectY);
                break;
            case 0x55:
                Eor(AddressingMode.ZeropageX);
                break;
            case 0x56:
                Lsr(AddressingMode.ZeropageX);
                break;
            case 0x58:
                Cli();
                break;
            case 0x59:
                Eor(AddressingMode.AbsoluteY);
                break;
            case 0x5D:
                Eor(AddressingMode.AbsoluteX);
                break;
            case 0x5E:
                Lsr(AddressingMode.AbsoluteX);
                break;
            case 0x60:
                Rts();
                break;
            case 0x66:
                Ror(AddressingMode.Zeropage);
                break;
            case 0x6D:
                Adc(AddressingMode.Absolute);
                break;
            case 0x6E:
                Ror(AddressingMode.Absolute);
                break;
            case 0x68:
                Pla();
                break;
            case 0x6A:
                Ror(AddressingMode.Accumulator);
                break;
            case 0x70:
                Bvs();
                break;
            case 0x76:
                Ror(AddressingMode.ZeropageX);
                break;
            case 0x78:
                Sei();
                break;
            case 0x7E:
                Ror(AddressingMode.AbsoluteX);
                break;
            case 0x81:
                Sta(AddressingMode.IndirectX);
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
            case 0x88:
                Dey();
                break;
            case 0x8A:
                Txa();
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
            case 0x90:
                Bcc();
                break;
            case 0x91:
                Sta(AddressingMode.IndirectY);
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
            case 0x98:
                Tya();
                break;
            case 0x99:
                Sta(AddressingMode.AbsoluteY);
                break;
            case 0x9A:
                Txs();
                break;
            case 0x9D:
                Sta(AddressingMode.AbsoluteX);
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
            case 0xA4:
                Ldy(AddressingMode.Zeropage);
                break;
            case 0xA5:
                Lda(AddressingMode.Zeropage);
                break;
            case 0xA6:
                Ldx(AddressingMode.Zeropage);
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
            case 0xAC:
                Ldy(AddressingMode.Absolute);
                break;
            case 0xAD:
                Lda(AddressingMode.Absolute);
                break;
            case 0xAE:
                Ldx(AddressingMode.Absolute);
                break;
            case 0xB0:
                Bcs();
                break;
            case 0xB1:
                Lda(AddressingMode.IndirectY);
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
            case 0xB8:
                Clv();
                break;
            case 0xB9:
                Lda(AddressingMode.AbsoluteY);
                break;
            case 0xBA:
                Tsx();
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
            case 0xC0:
                Cpy(AddressingMode.Immediate);
                break;
            case 0xC1:
                Cmp(AddressingMode.IndirectX);
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
            case 0xC8:
                Iny();
                break;
            case 0xC9:
                Cmp(AddressingMode.Immediate);
                break;
            case 0xCA:
                Dex();
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
            case 0xD0:
                Bne();
                break;
            case 0xD1:
                Cmp(AddressingMode.IndirectY);
                break;
            case 0xD5:
                Cmp(AddressingMode.ZeropageX);
                break;
            case 0xD6:
                Dec(AddressingMode.ZeropageX);
                break;
            case 0xD8:
                Cld();
                break;
            case 0xD9:
                Cmp(AddressingMode.AbsoluteY);
                break;
            case 0xDD:
                Cmp(AddressingMode.AbsoluteX);
                break;
            case 0xDE:
                Dec(AddressingMode.AbsoluteX);
                break;
            case 0xE0:
                Cpx(AddressingMode.Immediate);
                break;
            case 0xE4:
                Cpx(AddressingMode.Zeropage);
                break;
            case 0xE6:
                Inc(AddressingMode.Zeropage);
                break;
            case 0xE8:
                Inx();
                break;
            case 0xEA:
                Nop();
                break;
            case 0xEC:
                Cpx(AddressingMode.Absolute);
                break;
            case 0xEE:
                Inc(AddressingMode.Absolute);
                break;
            case 0xF0:
                Beq();
                break;
            case 0xF6:
                Inc(AddressingMode.ZeropageX);
                break;
            case 0xF8:
                Sed();
                break;
            case 0xFE:
                Inc(AddressingMode.AbsoluteX);
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
        // BCD mode
        var carry = Registers.P & (byte)StatusRegisterFlags.Carry;

        var low = (Registers.A & 0x0F) + (value & 0x0F) + carry;
        var halfCarry = low > 0x09;

        var high = (Registers.A >> 4) + (value >> 4) + (halfCarry ? 1 : 0);

        // Raw result before any correction, used for N and V flags
        var rawResult = (byte)((low & 0x0F) | ((high & 0x0F) << 4));

        if (halfCarry)
        {
            low += 0x06;
        }

        if (high > 0x09)
        {
            high += 0x06;
        }

        if (high > 0x0F)
        {
            Registers.P |= (byte)StatusRegisterFlags.Carry;
        }
        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Carry);
        }

        if (((Registers.A ^ rawResult) & (value ^ rawResult) & 0x80) != 0)
        {
            Registers.P |= (byte)StatusRegisterFlags.Overflow;
        }
        else
        {
            Registers.P &= unchecked((byte)~StatusRegisterFlags.Overflow);
        }

        Registers.A = (byte)((low & 0x0F) | ((high & 0x0F) << 4));
        Registers.SetNzFlags(rawResult);
    }

    #region instructions

    private void Adc(AddressingMode addressingMode)
    {
        var value = _bus.Read(GetPtr(addressingMode));

        if (addressingMode == AddressingMode.Absolute)
        {
            if ((Registers.P & (byte)StatusRegisterFlags.Decimal) != 0)
            {
                AdcDecimal(value);
            }
            else
            {
                AdcBinary(value);
            }
        }
    }

    private void And(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.A = (byte)(Registers.A & FetchByte());
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);

            Registers.A = (byte)(Registers.A & _bus.Read(ptr));
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }
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

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | 0x0));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            _clock += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var newCarry = (byte)(Registers.A >> 7);

            Registers.A = (byte)(Registers.A << 1 | 0x0);

            // Clear carry before setting it.
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | 0x0));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var newCarry = (byte)(value >> 7);

            _bus.Write(ptr, (byte)(value << 1 | 0x0));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            _clock += 6;
        }
    }

    private void Bcc()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Carry) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
            }
        }
    }

    private void Bcs()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Carry) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
            }
        }
    }

    private void Beq()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Zero) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
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

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var value = _bus.Read(GetPtr(addressingMode));
            var result = (byte)(Registers.A & value);

            const StatusRegisterFlags flags = (StatusRegisterFlags.Negative | StatusRegisterFlags.Overflow);

            Registers.P = (byte)((Registers.P & unchecked((byte)~flags)) | (value & (byte)flags));
            Registers.SetPFlag(result == 0 ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Zero);

            _clock += 4;
        }
    }

    private void Bmi()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Negative) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
            }
        }
    }

    private void Bne()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Zero) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
            }
        }
    }

    private void Bpl()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Negative) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
            }
        }
    }

    private void Brk()
    {
        var pc = (ushort)(Registers.Pc + 1);
        var pcLow = (byte)(pc & 0xFF);
        var pcHigh = (byte)((pc >> 8) & 0xFF);

        _bus.Write((ushort)(0x0100 + Registers.Sp), pcHigh);
        Registers.Sp--;
        _bus.Write((ushort)(0x0100 + Registers.Sp), pcLow);
        Registers.Sp--;

        var pushP = (byte)(Registers.P
                           | (byte)StatusRegisterFlags.Break
                           | (byte)StatusRegisterFlags.Ignored);

        _bus.Write((ushort)(0x0100 + Registers.Sp), pushP);
        Registers.Sp--;

        Registers.P |= (byte)StatusRegisterFlags.Irq;

        var pLow = _bus.Read(0xFFFE);
        var pHigh = _bus.Read(0xFFFF);
        Registers.Pc = (ushort)((pHigh << 8) | pLow);

        _clock += 7;
    }

    private void Bvc()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Overflow) == 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
            }
        }
    }

    private void Bvs()
    {
        var offset = (sbyte)FetchByte();
        _clock += 2;

        if ((Registers.P & (byte)StatusRegisterFlags.Overflow) != 0)
        {
            var oldPc = Registers.Pc;
            Registers.Pc = (ushort)(Registers.Pc + offset);

            _clock += 1;

            if ((oldPc & 0xFF00) != (Registers.Pc & 0xFF00))
            {
                _clock++;
            }
        }
    }

    private void Clc()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Carry);

        _clock += 2;
    }

    private void Cld()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Decimal);

        _clock += 2;
    }

    private void Cli()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);

        _clock += 2;
    }

    private void Clv()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Overflow);

        _clock += 2;
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

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.A - value);

            Registers.SetPFlag(Registers.A >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 4;
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

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();
            var result = (byte)(Registers.X - value); // Y - Bus

            Registers.SetPFlag(Registers.X >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.X - value);

            Registers.SetPFlag(Registers.X >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 3;
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

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();
            var result = (byte)(Registers.Y - value); // Y - Bus

            Registers.SetPFlag(Registers.Y >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);
            var result = (byte)(Registers.Y - value);

            Registers.SetPFlag(Registers.Y >= value ? BitOperation.Set : BitOperation.Clear, StatusRegisterFlags.Carry);
            Registers.SetNzFlags(result);

            _clock += 3;
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

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value - 1));
            Registers.SetNzFlags((byte)(value - 1));

            _clock += 7;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value - 1));
            Registers.SetNzFlags((byte)(value - 1));

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value - 1));
            Registers.SetNzFlags((byte)(value - 1));

            _clock += 6;
        }
    }

    private void Dex()
    {
        Registers.X = (byte)(Registers.X - 1);
        Registers.SetNzFlags(Registers.X);

        _clock += 2;
    }

    private void Dey()
    {
        Registers.Y = (byte)(Registers.Y - 1);
        Registers.SetNzFlags(Registers.Y);

        _clock += 2;
    }

    private void Eor(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A ^ value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
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

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value + 1));
            Registers.SetNzFlags((byte)(value + 1));

            _clock += 7;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value + 1));
            Registers.SetNzFlags((byte)(value + 1));

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            _bus.Write(ptr, (byte)(value + 1));
            Registers.SetNzFlags((byte)(value + 1));

            _clock += 6;
        }
    }

    private void Inx()
    {
        Registers.X = (byte)(Registers.X + 1);
        Registers.SetNzFlags(Registers.X);

        _clock += 2;
    }

    private void Iny()
    {
        Registers.Y = (byte)(Registers.Y + 1);
        Registers.SetNzFlags(Registers.Y);

        _clock += 2;
    }

    private void Jmp(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.Pc = GetPtr(addressingMode);

            _clock += 3;
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

            _clock += 5;
        }
    }

    private void Jsr()
    {
        var pcLow = FetchByte();
        var stackHigh = (byte)((Registers.Pc >> 8) & 0xFF);
        var stackLow = (byte)(Registers.Pc & 0xFF);

        _bus.Write((ushort)(0x0100 + Registers.Sp), stackHigh);
        Registers.Sp--;
        _bus.Write((ushort)(0x0100 + Registers.Sp), stackLow);
        Registers.Sp--;

        // SST 20 55 13: The high byte is read from the newly pushed
        // value to the stack, so you genuinely need to read the
        // operand high byte after pushing the data to the stack.
        var pcHigh = FetchByte();
        Registers.Pc = (ushort)((pcHigh << 8) | pcLow);

        _clock += 6;
    }

    private void Lda(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);

            // 5 if page crossed
            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);

            // 5 if page crossed
            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.A = FetchByte();
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            Registers.A = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }
    }

    private void Ldx(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.X);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.X);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.X = FetchByte();
            Registers.SetNzFlags(Registers.X);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.X);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageY)
        {
            Registers.X = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.X);

            _clock += 4;
        }
    }

    private void Ldy(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            Registers.Y = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.Y);

            _clock += 4;
        }

        if (addressingMode is AddressingMode.AbsoluteX)
        {
            Registers.Y = _bus.Read(GetPtr(addressingMode));
            Registers.SetNzFlags(Registers.Y);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            Registers.Y = FetchByte();
            Registers.SetNzFlags(Registers.Y);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            Registers.Y = _bus.Read(ptr);
            Registers.SetNzFlags(Registers.Y);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            Registers.Y = _bus.Read(ptr);
            Registers.SetNzFlags(Registers.Y);

            _clock += 4;
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

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 & ~0x80));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            _clock += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var newCarry = (byte)(Registers.A & 0x1);

            Registers.A = (byte)(Registers.A >> 1 & ~0x80);
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 & ~0x80));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            var newCarry = (byte)(value & 0x1);

            _bus.Write(ptr, (byte)(value >> 1 & ~0x80));

            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(_bus.Read(ptr));

            _clock += 6;
        }
    }

    private void Nop()
    {
        _clock += 2;
    }

    private void Ora(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Immediate)
        {
            var value = FetchByte();

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 5;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            var ptr = GetPtr(addressingMode);
            var value = _bus.Read(ptr);

            Registers.A = (byte)(Registers.A | value);
            Registers.SetNzFlags(Registers.A);

            _clock += 4;
        }
    }

    private void Pha()
    {
        _bus.Write((ushort)(0x0100 + Registers.Sp), Registers.A);
        Registers.Sp -= 1;

        _clock += 3;
    }

    private void Php()
    {
        var processorStatus = (byte)(Registers.P | (1 << 4));
        _bus.Write((ushort)(0x0100 + Registers.Sp), processorStatus);
        Registers.Sp -= 1;

        _clock += 3;
    }

    private void Pla()
    {
        Registers.Sp += 1;
        Registers.A = _bus.Read((ushort)(0x0100 + Registers.Sp));
        Registers.SetNzFlags(Registers.A);

        _clock += 4;
    }

    private void Plp()
    {
        Registers.Sp += 1;

        var processorStatus = _bus.Read((ushort)(0x100 + Registers.Sp));
        const StatusRegisterFlags statusRegisterFlags =
            StatusRegisterFlags.Carry | StatusRegisterFlags.Zero | StatusRegisterFlags.Irq |
            StatusRegisterFlags.Decimal | StatusRegisterFlags.Overflow |
            StatusRegisterFlags.Negative;

        Registers.P =
            (byte)((Registers.P & unchecked((byte)~statusRegisterFlags)) |
                   (processorStatus & (byte)statusRegisterFlags));

        _clock += 4;
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

            _clock += 6;
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

            _clock += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(Registers.A >> 7);

            Registers.A = (byte)(Registers.A << 1 | oldCarry);

            // Clear carry before setting it.
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
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

            _clock += 5;
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

            _clock += 6;
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

            _clock += 6;
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

            _clock += 7;
        }

        else if (addressingMode is AddressingMode.Accumulator)
        {
            var oldCarry = (byte)(Registers.P & (byte)StatusRegisterFlags.Carry);
            var newCarry = (byte)(Registers.A & 0x1);

            Registers.A = (byte)(Registers.A >> 1 | (oldCarry << 7));

            // Clear carry before setting it.
            Registers.P = (byte)((Registers.P & ~(byte)StatusRegisterFlags.Carry) | newCarry);
            Registers.SetNzFlags(Registers.A);

            _clock += 2;
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

            _clock += 5;
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

            _clock += 6;
        }
    }

    private void Rti()
    {
        //pull NVxxDIZC flags from stack
        // pull PC low byte from stack
        // pull PC high byte from stack 

        Registers.Sp++;
        var pFlags = _bus.Read((ushort)(0x100 + Registers.Sp));

        const StatusRegisterFlags statusRegisterFlags =
            StatusRegisterFlags.Carry | StatusRegisterFlags.Zero | StatusRegisterFlags.Irq |
            StatusRegisterFlags.Decimal | StatusRegisterFlags.Overflow |
            StatusRegisterFlags.Negative;

        Registers.P =
            (byte)((Registers.P & unchecked((byte)~statusRegisterFlags)) |
                   (pFlags & (byte)statusRegisterFlags));

        Registers.Sp++;
        var pcLow = _bus.Read((ushort)(0x100 + Registers.Sp));
        Registers.Sp++;
        var pcHigh = _bus.Read((ushort)(0x100 + Registers.Sp));

        Registers.Pc = (ushort)((pcHigh << 8) | pcLow);

        _clock += 6;
    }

    private void Rts()
    {
        Registers.Sp++;
        var pcLow = _bus.Read((ushort)(0x0100 + Registers.Sp));
        Registers.Sp++;
        var pcHigh = _bus.Read((ushort)(0x0100 + Registers.Sp));
        Registers.Pc = (ushort)((pcHigh << 8) | pcLow);
        Registers.Pc = (ushort)(Registers.Pc + 1);

        _clock += 6;
    }

    private void Sec()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Carry);

        _clock += 2;
    }

    private void Sed()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Decimal);

        _clock += 2;
    }

    private void Sei()
    {
        Registers.SetPFlag(BitOperation.Set, StatusRegisterFlags.Irq);

        _clock += 2;
    }

    private void Sta(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.AbsoluteY)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.IndirectX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.IndirectY)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);

            _clock += 6;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.A);

            _clock += 4;
        }
    }

    private void Stx(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            _bus.Write(GetPtr(addressingMode), Registers.X);

            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            _bus.Write(GetPtr(addressingMode), Registers.X);

            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageY)
        {
            _bus.Write(GetPtr(addressingMode), Registers.X);
            _clock += 4;
        }
    }

    private void Sty(AddressingMode addressingMode)
    {
        if (addressingMode is AddressingMode.Absolute)
        {
            _bus.Write(GetPtr(addressingMode), Registers.Y);
            _clock += 4;
        }

        else if (addressingMode is AddressingMode.Zeropage)
        {
            _bus.Write(GetPtr(addressingMode), Registers.Y);
            _clock += 3;
        }

        else if (addressingMode is AddressingMode.ZeropageX)
        {
            _bus.Write(GetPtr(addressingMode), Registers.Y);
            _clock += 4;
        }
    }

    private void Tax()
    {
        Registers.X = Registers.A;
        Registers.SetNzFlags(Registers.X);

        _clock += 2;
    }

    private void Tay()
    {
        Registers.Y = Registers.A;
        Registers.SetNzFlags(Registers.Y);

        _clock += 2;
    }

    private void Tsx()
    {
        Registers.X = Registers.Sp;
        Registers.SetNzFlags(Registers.X);

        _clock += 2;
    }

    private void Txa()
    {
        Registers.A = Registers.X;
        Registers.SetNzFlags(Registers.A);

        _clock += 2;
    }

    private void Txs()
    {
        Registers.Sp = Registers.X;

        _clock += 2;
    }

    private void Tya()
    {
        Registers.A = Registers.Y;
        Registers.SetNzFlags(Registers.A);

        _clock += 2;
    }

    #endregion
}