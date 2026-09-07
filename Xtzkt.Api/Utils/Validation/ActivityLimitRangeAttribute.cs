namespace Xtzkt.Api.Utils.Validation;

public class ActivityLimitRangeAttribute() : RangeAttribute(1)
{
    public override int Maximum => ApiConfig.MaxActivityLimit;
}
