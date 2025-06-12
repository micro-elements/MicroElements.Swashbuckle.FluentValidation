namespace MicroElements.Swashbuckle.FluentValidation.Tests.Samples;

public class Sample
{
    public string PropertyWithNoRules { get; set; }

    public string NotNull { get; set; }
    public string NotEmpty { get; set; }
    public string EmailAddressRegex { get; set; }
    public string EmailAddress { get; set; }
    public string RegexField { get; set; }

    public int ValueInRange { get; set; }
    public int ValueInRangeExclusive { get; set; }

    public float ValueInRangeFloat { get; set; }
    public double ValueInRangeDouble { get; set; }
    public decimal DecimalValue { get; set; }

    public string NotEmptyWithMaxLength { get; set; }

    // ReSharper disable once InconsistentNaming
    // https://github.com/micro-elements/MicroElements.Swashbuckle.FluentValidation/issues/10
    public string javaStyleProperty { get; set; }
}
