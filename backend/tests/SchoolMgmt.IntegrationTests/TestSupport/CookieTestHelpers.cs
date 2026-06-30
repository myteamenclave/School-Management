namespace SchoolMgmt.IntegrationTests.TestSupport;

// WebApplicationFactory's HttpClient doesn't persist cookies between requests
// like a browser — tests extract Set-Cookie from one response and attach it
// as a Cookie header on the next request explicitly.
public static class CookieTestHelpers
{
    public static string BuildCookieHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            return string.Empty;

        return string.Join("; ", setCookieHeaders.Select(h => h.Split(';')[0]));
    }

    public static HttpRequestMessage WithCookies(this HttpRequestMessage request, string cookieHeader)
    {
        if (!string.IsNullOrEmpty(cookieHeader))
            request.Headers.Add("Cookie", cookieHeader);
        return request;
    }
}
