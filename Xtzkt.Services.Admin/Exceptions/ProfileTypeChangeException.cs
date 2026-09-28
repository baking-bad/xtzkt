namespace Xtzkt.Services.Admin.Exceptions;

public class ProfileTypeChangeException(string id) : Exception($"Type of profile {id} can't be changed")
{
    public string Id { get; } = id;
}
