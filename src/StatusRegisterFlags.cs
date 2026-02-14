namespace V6502;

[Flags]
public enum StatusRegisterFlags
{
    Carry = (1 << 0),
    Zero = (1 << 1),
    Irq = (1 << 2),
    Decimal = (1 << 3),
    Break = (1 << 4),
    Ignored = (1 << 5),
    Overflow = (1 << 6),
    Negative = (1 << 7),
}