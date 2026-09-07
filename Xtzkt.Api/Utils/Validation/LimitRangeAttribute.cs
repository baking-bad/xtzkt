namespace Xtzkt.Api.Utils.Validation;

public class LimitRangeAttribute() : RangeAttribute(1)
{
    public override int Maximum => ApiConfig.MaxLimit;
}
