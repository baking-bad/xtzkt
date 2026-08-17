namespace Microsoft.AspNetCore.Mvc;

static class ControllerBaseExtension
{
    public static ActionResult Bytes(this ControllerBase controller, byte[] bytes)
    {
        return controller.File(bytes, "application/json; charset=utf-8");
    }
}
