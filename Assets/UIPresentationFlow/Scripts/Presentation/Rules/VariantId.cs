using System;

public readonly struct VariantId : IEquatable<VariantId>
{
    public readonly string Value;

    public VariantId(string value)
    {
        Value = value ?? string.Empty;
    }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public bool Equals(VariantId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override bool Equals(object obj)
        => obj is VariantId other && Equals(other);

    public override int GetHashCode()
        => Value != null ? Value.GetHashCode() : 0;

    public static bool operator ==(VariantId left, VariantId right)
        => left.Equals(right);

    public static bool operator !=(VariantId left, VariantId right)
        => !left.Equals(right);

    public override string ToString() => Value;

    public static implicit operator VariantId(string value)
        => new VariantId(value);

    public static implicit operator string(VariantId id)
        => id.Value;
}