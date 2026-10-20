// Coarse platform class consumed by presentation rules.
// Deliberately not UnityEngine.RuntimePlatform: rules should not depend on
// vendor-level detail, and tests should not need Unity enum values.
public enum DisplayPlatform
{
    Unknown = 0,
    Desktop,
    Mobile,
    Console,
}
