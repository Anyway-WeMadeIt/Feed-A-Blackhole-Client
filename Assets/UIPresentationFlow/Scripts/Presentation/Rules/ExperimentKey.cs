using System;

public static class UIExperiments
{
    public static readonly ExperimentKey HomeLayoutTest
        = new ExperimentKey("HomeLayoutTest");
}

[Serializable]
public readonly struct ExperimentKey : IEquatable<ExperimentKey>
{
    public readonly string Value;

    public ExperimentKey(string value)
    {
        Value = value ?? string.Empty;
    }

    public bool IsValid => !string.IsNullOrEmpty(Value);

    public override string ToString() => Value;

    public bool Equals(ExperimentKey other) => Value == other.Value;
    public override bool Equals(object obj) => obj is ExperimentKey other && Equals(other);
    public override int GetHashCode() => Value.GetHashCode();

    public static implicit operator ExperimentKey(string value) => new(value);
    public static implicit operator string(ExperimentKey key) => key.Value;
}