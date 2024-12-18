using System.Text.Json.Serialization;

namespace MoyuLinuxdo
{
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(LatestResponse))]
    [JsonSerializable(typeof(TopicResponse))]
    [JsonSerializable(typeof(PostsResponse))]
    [JsonSerializable(typeof(AboutResponse))]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(SearchResponse))]
    [JsonSerializable(typeof(RecentSearchResponse))]
    internal partial class AppJsonContext : JsonSerializerContext
    {
    }
} 