using System.Text.Json.Serialization;

namespace MoyuLinuxdo
{
    public class LatestResponse
    {
        [JsonPropertyName("users")]
        public List<User>? Users { get; set; }

        [JsonPropertyName("topic_list")]
        public TopicList? TopicList { get; set; }
    }

    public class TopicList
    {
        [JsonPropertyName("topics")]
        public List<Topic>? Topics { get; set; }
    }

    public class Topic
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("pinned")]
        public bool Pinned { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("has_accepted_answer")]
        public bool HasAcceptedAnswer { get; set; }

        [JsonPropertyName("posters")]
        public List<Poster>? Posters { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }
    }

    public class Poster
    {
        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }
    }

    public class User
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class TopicResponse
    {
        [JsonPropertyName("post_stream")]
        public PostStream? PostStream { get; set; }
    }

    public class PostStream
    {
        [JsonPropertyName("stream")]
        public List<int>? Stream { get; set; }
    }

    public class PostsResponse
    {
        [JsonPropertyName("post_stream")]
        public PostStreamData? PostStream { get; set; }
    }

    public class PostStreamData
    {
        [JsonPropertyName("posts")]
        public List<Post>? Posts { get; set; }
    }

    public class Post
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("cooked")]
        public string? Cooked { get; set; }

        [JsonPropertyName("reads")]
        public int Reads { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("flair_name")]
        public string? FlairName { get; set; }

        [JsonPropertyName("reactions")]
        public List<Reaction>? Reactions { get; set; }

        [JsonPropertyName("link_counts")]
        public List<LinkCount>? LinkCounts { get; set; }

        [JsonPropertyName("trust_level")]
        public int TrustLevel { get; set; }

        [JsonPropertyName("reaction_users_count")]
        public int? ReactionUsersCount { get; set; }

        [JsonPropertyName("admin")]
        public bool? Admin { get; set; }
    }

    public class Reaction
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class Category
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("color")]
        public string? Color { get; set; }
    }

    public class AboutResponse
    {
        [JsonPropertyName("categories")]
        public List<Category>? Categories { get; set; }
    }

    public class LinkCount
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("clicks")]
        public int Clicks { get; set; }
    }

    public class RecentSearchResponse
    {
        [JsonPropertyName("success")]
        public string? Success { get; set; }

        [JsonPropertyName("recent_searches")]
        public List<string>? RecentSearches { get; set; }
    }

    public class SearchResponse
    {
        [JsonPropertyName("posts")]
        public List<SearchPost>? Posts { get; set; }

        [JsonPropertyName("users")]
        public List<SearchUser>? Users { get; set; }

        [JsonPropertyName("topics")]
        public List<SearchTopic>? Topics { get; set; }
    }

    public class SearchPost
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("blurb")]
        public string? Blurb { get; set; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }

        [JsonPropertyName("topic_id")]
        public int TopicId { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("like_count")]
        public int LikeCount { get; set; }
    }

    public class SearchUser
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class SearchTopic
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("posts_count")]
        public int PostsCount { get; set; }

        [JsonPropertyName("reply_count")]
        public int ReplyCount { get; set; }

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("last_posted_at")]
        public DateTime LastPostedAt { get; set; }

        [JsonPropertyName("tags")]
        public List<string>? Tags { get; set; }

        [JsonPropertyName("category_id")]
        public int CategoryId { get; set; }
    }
}
