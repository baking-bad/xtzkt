using System.Net.Http.Headers;

namespace Xtzkt.Utils.Network;

public sealed class JsonContent(string content) : StringContent(content, new MediaTypeHeaderValue("application/json")) { }