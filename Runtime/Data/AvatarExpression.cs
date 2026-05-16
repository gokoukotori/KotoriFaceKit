namespace Aoyon.FaceTune;

internal class AvatarExpression : IEquatable<AvatarExpression> // 可変
{
    public string Name { get; private set; }
    public AnimationSet AnimationSet;
    
    private ExpressionSettings _expressionSettings;
    public ExpressionSettings ExpressionSettings { get => _expressionSettings; private set => _expressionSettings = value; }
    private FacialSettings _facialSettings;
    public FacialSettings FacialSettings { get => _facialSettings; private set => _facialSettings = value; }
    private List<ParameterDriverSettings> _parameterDrivers;
    public IReadOnlyList<ParameterDriverSettings> ParameterDrivers => _parameterDrivers;


    public AvatarExpression(
        string name,
        IEnumerable<GenericAnimation> animations,
        ExpressionSettings expressionSettings,
        FacialSettings? settings = null,
        IEnumerable<ParameterDriverSettings>? parameterDrivers = null)
    {
        Name = name;
        AnimationSet = new AnimationSet(animations);
        _expressionSettings = expressionSettings;
        _facialSettings = settings ?? FacialSettings.Keep;
        _parameterDrivers = parameterDrivers?.Select(driver => new ParameterDriverSettings(driver.LocalOnly, driver.Operations)).ToList()
            ?? new List<ParameterDriverSettings>();
    }
    
    public void MergeExpression(AvatarExpression other)
    {
        MergeAnimation(other.AnimationSet);
        MergeExpressionSettings(other.ExpressionSettings);
        MergeFacialSettings(other.FacialSettings);
        MergeParameterDrivers(other.ParameterDrivers);
    }
    public void MergeAnimation(IEnumerable<GenericAnimation> others) => AnimationSet.MergeAnimation(others);
    public void MergeExpressionSettings(ExpressionSettings other) => _expressionSettings = _expressionSettings.Merge(other);
    public void MergeFacialSettings(FacialSettings other) => _facialSettings = _facialSettings.Merge(other);
    public void MergeParameterDrivers(IEnumerable<ParameterDriverSettings> others)
    {
        _parameterDrivers.AddRange(others.Select(driver => new ParameterDriverSettings(driver.LocalOnly, driver.Operations)));
    }

    public bool Equals(AvatarExpression other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && AnimationSet.SequenceEqual(other.AnimationSet)
            && FacialSettings == other.FacialSettings
            && ExpressionSettings == other.ExpressionSettings
            && ParameterDrivers.SequenceEqual(other.ParameterDrivers);
    }

    public override bool Equals(object? obj)
    {
        return obj is AvatarExpression expression && Equals(expression);
    }

    public override int GetHashCode()
    {
        var hash = Name.GetHashCode();
        hash ^= AnimationSet.GetSequenceHashCode();
        hash ^= FacialSettings.GetHashCode();
        hash ^= ExpressionSettings.GetHashCode();
        hash ^= ParameterDrivers.GetSequenceHashCode();
        return hash;
    }
}
