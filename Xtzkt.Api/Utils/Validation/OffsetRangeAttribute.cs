namespace Xtzkt.Api.Utils.Validation;

public class OffsetRangeAttribute() : RangeAttribute(0)
{
    public override int Maximum => ApiConfig.MaxOffset;
}
