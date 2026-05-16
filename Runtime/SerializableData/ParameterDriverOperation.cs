namespace Aoyon.FaceTune;

public enum ParameterDriverChangeType
{
    Set,
    Add,
    Random,
    Copy
}

[Serializable]
public record class ParameterDriverOperation
{
    [SerializeField] private ParameterDriverChangeType type;
    public ParameterDriverChangeType Type { get => type; init => type = value; }
    public const string TypePropName = nameof(type);

    [SerializeField] private string destination;
    public string Destination { get => destination; init => destination = value; }
    public const string DestinationPropName = nameof(destination);

    [SerializeField] private float value;
    public float Value { get => value; init => this.value = value; }
    public const string ValuePropName = nameof(value);

    [SerializeField] private float valueMin;
    public float ValueMin { get => valueMin; init => valueMin = value; }
    public const string ValueMinPropName = nameof(valueMin);

    [SerializeField] private float valueMax;
    public float ValueMax { get => valueMax; init => valueMax = value; }
    public const string ValueMaxPropName = nameof(valueMax);

    [SerializeField] private string source;
    public string Source { get => source; init => source = value; }
    public const string SourcePropName = nameof(source);

    [SerializeField] private float chance;
    public float Chance { get => chance; init => chance = value; }
    public const string ChancePropName = nameof(chance);

    [SerializeField] private bool preventRepeats;
    public bool PreventRepeats { get => preventRepeats; init => preventRepeats = value; }
    public const string PreventRepeatsPropName = nameof(preventRepeats);

    [SerializeField] private bool convertRange;
    public bool ConvertRange { get => convertRange; init => convertRange = value; }
    public const string ConvertRangePropName = nameof(convertRange);

    [SerializeField] private float sourceMin;
    public float SourceMin { get => sourceMin; init => sourceMin = value; }
    public const string SourceMinPropName = nameof(sourceMin);

    [SerializeField] private float sourceMax;
    public float SourceMax { get => sourceMax; init => sourceMax = value; }
    public const string SourceMaxPropName = nameof(sourceMax);

    [SerializeField] private float destinationMin;
    public float DestinationMin { get => destinationMin; init => destinationMin = value; }
    public const string DestinationMinPropName = nameof(destinationMin);

    [SerializeField] private float destinationMax;
    public float DestinationMax { get => destinationMax; init => destinationMax = value; }
    public const string DestinationMaxPropName = nameof(destinationMax);

    public ParameterDriverOperation()
    {
        type = ParameterDriverChangeType.Set;
        destination = string.Empty;
        value = 0;
        valueMin = 0;
        valueMax = 1;
        source = string.Empty;
        chance = 1;
        preventRepeats = false;
        convertRange = false;
        sourceMin = 0;
        sourceMax = 1;
        destinationMin = 0;
        destinationMax = 1;
    }

    public static ParameterDriverOperation Set(string destination, float value)
    {
        return new ParameterDriverOperation
        {
            type = ParameterDriverChangeType.Set,
            destination = destination,
            value = value
        };
    }

    public static ParameterDriverOperation Add(string destination, float value)
    {
        return new ParameterDriverOperation
        {
            type = ParameterDriverChangeType.Add,
            destination = destination,
            value = value
        };
    }

    public static ParameterDriverOperation Random(string destination, float min, float max, float chance = 1, bool preventRepeats = false)
    {
        return new ParameterDriverOperation
        {
            type = ParameterDriverChangeType.Random,
            destination = destination,
            valueMin = min,
            valueMax = max,
            chance = chance,
            preventRepeats = preventRepeats
        };
    }

    public static ParameterDriverOperation Copy(
        string source,
        string destination,
        bool convertRange = false,
        float sourceMin = 0,
        float sourceMax = 1,
        float destinationMin = 0,
        float destinationMax = 1)
    {
        return new ParameterDriverOperation
        {
            type = ParameterDriverChangeType.Copy,
            source = source,
            destination = destination,
            convertRange = convertRange,
            sourceMin = sourceMin,
            sourceMax = sourceMax,
            destinationMin = destinationMin,
            destinationMax = destinationMax
        };
    }
}

internal class ParameterDriverSettings : IEquatable<ParameterDriverSettings>
{
    public bool LocalOnly { get; }
    public IReadOnlyList<ParameterDriverOperation> Operations { get; }

    public ParameterDriverSettings(bool localOnly, IEnumerable<ParameterDriverOperation> operations)
    {
        LocalOnly = localOnly;
        Operations = operations.Select(operation => operation with { }).ToArray();
    }

    public virtual bool Equals(ParameterDriverSettings? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return LocalOnly == other.LocalOnly && Operations.SequenceEqual(other.Operations);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(LocalOnly, Operations.GetSequenceHashCode());
    }
}
