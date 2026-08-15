using System.Net.Http.Headers;

namespace Xtzkt.Indexers.Common.Utils;

public class JsonContent(string content) : StringContent(content, new MediaTypeHeaderValue("application/json")) { }