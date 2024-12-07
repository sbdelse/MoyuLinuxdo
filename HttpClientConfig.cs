namespace MoyuLinuxdo
{
    public static class HttpClientConfig
    {
        public static readonly Dictionary<string, string> DefaultHeaders = new()
        {
            ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36",
            ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
            ["Accept-Language"] = "en-US,en;q=0.9,zh-CN;q=0.8,zh;q=0.7",
            ["Accept-Encoding"] = "gzip, deflate, br",
            ["Connection"] = "keep-alive",
            ["Upgrade-Insecure-Requests"] = "1",
            ["Sec-Ch-Ua"] = "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"122\"",
            ["Sec-Ch-Ua-Mobile"] = "?0",
            ["Sec-Ch-Ua-Platform"] = "\"Windows\"",
            ["Sec-Fetch-Dest"] = "document",
            ["Sec-Fetch-Mode"] = "navigate",
            ["Sec-Fetch-Site"] = "none",
            ["Sec-Fetch-User"] = "?1"
        };

        public static void ApplyDefaultHeaders(HttpClient client)
        {
            foreach (var header in DefaultHeaders)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }
        }
    }
} 