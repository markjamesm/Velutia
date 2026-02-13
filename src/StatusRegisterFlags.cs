namespace V6502;

[Flags]
public enum StatusRegisterFlags
{
    Carry = 1,
    Zero = 2,
    Irq = 4,
    Decimal = 8,
    Break = 16,
    Ignored = 32,
    Overflow = 64,
    Negative = 128,
}