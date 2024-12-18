﻿using System.Text.Json;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using MoyuLinuxdo.Helpers;

namespace MoyuLinuxdo
{
    class MoyuApp
    {
        private readonly HttpClient client;
        private AppSettings settings;
        private List<Topic>? topics;
        private Dictionary<int, User>? usersDict;
        private List<List<Topic>> loadedTopicPages = new List<List<Topic>>();
        private int currentTopicPage = 0;
        private Dictionary<int, string> categoryNames = new Dictionary<int, string>();
        private readonly JsonSerializerOptions _jsonOptions;
        private AboutResponse? cachedAboutData;
        private DateTime lastAboutDataUpdate = DateTime.MinValue;
        private const int ABOUT_CACHE_MINUTES = 120;
        private string? lastSearchTerm;
        private int lastSearchPage;
        private bool lastSearchWasAi;
        private bool returnToSearch;

        public MoyuApp()
        {
            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip 
                    | System.Net.DecompressionMethods.Deflate 
                    | System.Net.DecompressionMethods.Brotli
            };
            client = new HttpClient(handler);
            
            HttpClientConfig.ApplyDefaultHeaders(client);

            _jsonOptions = new JsonSerializerOptions
            {
                TypeInfoResolver = AppJsonContext.Default
            };

            settings = LoadSettings();
            if (!string.IsNullOrEmpty(settings.Cookie))
            {
                client.DefaultRequestHeaders.Remove("Cookie");
                client.DefaultRequestHeaders.Add("Cookie", settings.Cookie);
            }

            LoadCategories().Wait();
        }

        public async Task Run()
        {
            bool exitProgram = false;
            while (!exitProgram)
            {
                Console.Clear();
                bool topicsLoaded = await LoadTopicPage(currentTopicPage);
                if (!topicsLoaded)
                {
                    Console.WriteLine("\n无法加载主题列表。");
                    Console.WriteLine("1. 进入设置界面设置 Cookie");
                    Console.WriteLine("2. 重试加载");
                    Console.WriteLine("3. 退出程序");
                    Console.Write("请选择操作 (1-3): ");
                    
                    var key = Console.ReadKey(true);
                    switch (key.KeyChar)
                    {
                        case '1':
                            SettingsMenu();
                            continue;
                        case '2':
                            continue;
                        case '3':
                        default:
                            return;
                    }
                }

                DisplayTopics(currentTopicPage);
                Console.WriteLine("输入数字序号选择帖子，输入n下一页，p上一页，r刷新，f搜索，s进入设置，q退出");
                Console.Write("请输入命令：");
                string command = "";
                bool isNumberInput = false;

                while (true)
                {
                    var keyInfo = Console.ReadKey(true);
                    if (keyInfo.Key == ConsoleKey.Enter)
                    {
                        break;
                    }
                    else if (keyInfo.Key == ConsoleKey.Backspace)
                    {
                        if (command.Length > 0)
                        {
                            command = command[..^1];
                            Console.Write("\b \b");
                            isNumberInput = command.Length > 0;
                        }
                    }
                    else if (char.IsDigit(keyInfo.KeyChar))
                    {
                        command += keyInfo.KeyChar;
                        Console.Write(keyInfo.KeyChar);
                        isNumberInput = true;
                    }
                    else
                    {
                        char cmd = char.ToLower(keyInfo.KeyChar);
                        if (cmd == 's' || cmd == 'r' || cmd == 'q' || cmd == 'n' || cmd == 'p' || cmd == 'f')
                        {
                            command = cmd.ToString();
                            Console.Write(cmd);
                            break;
                        }
                    }
                }

                if (command == "q")
                {
                    exitProgram = true;
                }
                else if (command == "s")
                {
                    SettingsMenu();
                }
                else if (command == "r")
                {
                    await RefreshCurrentPage();
                }
                else if (command == "n")
                {
                    await NextTopicPage();
                }
                else if (command == "p")
                {
                    PreviousTopicPage();
                }
                else if (command == "f")
                {
                    await HandleSearch();
                }
                else if (isNumberInput && int.TryParse(command, out int topicIndex))
                {
                    int globalIndex = currentTopicPage * settings.TopicsPerPage + topicIndex;
                    if (topicIndex >= 1 && topicIndex <= (topics?.Count ?? 0))
                    {
                        await ViewTopic(topics![topicIndex - 1]);
                    }
                    else
                    {
                        Console.WriteLine("\n无效的命令，按任意键继续...");
                        Console.ReadKey();
                    }
                }
                else
                {
                    Console.WriteLine("\n无效的命令，按任意键继续...");
                    Console.ReadKey();
                }
            }
        }

        private async Task<bool> LoadTopicPage(int pageNumber)
        {
            int maxRetries = 3;
            int currentRetry = 0;
            int retryDelayMs = 2000;

            while (currentRetry < maxRetries)
            {
                try
                {
                    if (loadedTopicPages.Count > pageNumber)
                    {
                        topics = loadedTopicPages[pageNumber];
                        return true;
                    }

                    UpdateRequestHeaders();

                    string url = pageNumber == 0 ? "https://linux.do/latest.json" :
                        $"https://linux.do/latest.json?no_definitions=true&page={pageNumber}";
                    var response = await client.GetAsync(url);
                    
                    // Check if CF verification is required
                    if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        currentRetry++;
                        if (currentRetry < maxRetries)
                        {
                            Console.WriteLine($"\n遇到 Cloudflare 验证，等待 {retryDelayMs/1000} 秒后重试 ({currentRetry}/{maxRetries})...");
                            Console.WriteLine("提示：如果持续失败，以尝试在设置中配置有效的 Cookie");
                            await Task.Delay(retryDelayMs);
                            retryDelayMs *= 2;
                            continue;
                        }
                        return false;
                    }

                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    var latest = JsonSerializer.Deserialize<LatestResponse>(content, _jsonOptions);

                    if (latest?.TopicList?.Topics == null || latest.TopicList.Topics.Count == 0)
                    {
                        return false;
                    }

                    topics = latest.TopicList.Topics.Take(settings.TopicsPerPage).ToList();
                    loadedTopicPages.Add(topics);

                    usersDict = new Dictionary<int, User>();
                    if (latest.Users != null)
                    {
                        foreach (var user in latest.Users)
                        {
                            if (user != null)
                            {
                                usersDict[user.Id] = user;
                            }
                        }
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    currentRetry++;
                    if (currentRetry < maxRetries)
                    {
                        Console.WriteLine($"\n加载失败，等待 {retryDelayMs/1000} 秒后重试 ({currentRetry}/{maxRetries})...");
                        Console.WriteLine("提示：如果持续失败，可以尝试在设置中配置有效的 Cookie");
                        await Task.Delay(retryDelayMs);
                        retryDelayMs *= 2;
                    }
                    else
                    {
                        Console.WriteLine($"\n加载失败: {ex.Message}");
                        return false;
                    }
                }
            }
            return false;
        }

        private void UpdateRequestHeaders()
        {
            client.DefaultRequestHeaders.Clear();
            if (!string.IsNullOrEmpty(settings.Cookie))
            {
                client.DefaultRequestHeaders.Add("Cookie", settings.Cookie);
            }
        }

        private void DisplayTopics(int pageNumber)
{
    if (topics == null)
    {
        Console.WriteLine("没有可显示的主题。");
        return;
    }

    Console.WriteLine($"主页帖子列表 - 第 {pageNumber + 1} 页：");
    for (int i = 0; i < topics.Count; i++)
    {
        var topic = topics[i];
        string line = "";

        if (topic.Pinned)
        {
            line += "[TOP] ";
        }

        if (topic.CategoryId != 0 && categoryNames.TryGetValue(topic.CategoryId, out string? categoryName))
        {
            line += $"[{categoryName}] ";
        }

        if (topic.Tags != null && topic.Tags.Count > 0)
        {
            line += $"[{string.Join(",", topic.Tags)}] ";
        }

        string timeStr = GetTimeDifference(topic.CreatedAt);
        line += $"{timeStr} ";

        string title = topic.Title ?? "无标题";
        int maxTitleLength = Console.WindowWidth - line.Length - 10;
        if (title.Length > maxTitleLength && maxTitleLength > 0)
        {
            title = title.Substring(0, maxTitleLength - 3) + "...";
        }
        line += $"{title}";

        if (topic.HasAcceptedAnswer)
        {
            line += " [已解决]";
        }
        Console.WriteLine($"{i + 1}. {line}");
        }
    }

        private string GetTimeDifference(DateTime createdAt)
        {
            TimeSpan diff = DateTime.UtcNow - createdAt.ToUniversalTime();
            if (diff.TotalMinutes < 1)
            {
                return "刚刚";
            }
            else if (diff.TotalHours < 1)
            {
                return $"{(int)diff.TotalMinutes}分钟前";
            }
            else if (diff.TotalDays < 1)
            {
                return $"{(int)diff.TotalHours}小时前";
            }
            else
            {
                return $"{(int)diff.TotalDays}天前";
            }
        }

        private async Task ViewTopic(Topic topic)
        {
            bool wasFromSearch = returnToSearch;
            returnToSearch = true;

            int page = 0;
            bool exit = false;
            List<Post> cachedPosts = new List<Post>();
            bool isPreloading = false;
            int totalPosts = 0;
            string topicUrl = $"https://linux.do/t/topic/{topic.Id}.json";
            var topicResponse = await client.GetAsync(topicUrl);
            if (topicResponse.IsSuccessStatusCode)
            {
                var topicContent = await topicResponse.Content.ReadAsStringAsync();
                var topicData = JsonSerializer.Deserialize<TopicResponse>(topicContent, _jsonOptions);
                totalPosts = topicData?.PostStream?.Stream?.Count ?? 0;
            }

            bool initialLoad = await LoadPosts(topic.Id, cachedPosts, Math.Max(20, settings.RepliesPerTopic));
            if (!initialLoad)
            {
                Console.WriteLine("无法加载帖子内容。按任意键返回...");
                Console.ReadKey();
                return;
            }

            while (!exit)
            {
                Console.Clear();
                Console.WriteLine($"帖子：{topic.Title}");

                int start = page * settings.RepliesPerTopic;
                int end = Math.Min(start + settings.RepliesPerTopic, cachedPosts.Count);

                if (!isPreloading && end + settings.RepliesPerTopic >= cachedPosts.Count)
                {
                    isPreloading = true;
                    _ = PreloadNextBatch(topic.Id, cachedPosts)
                        .ContinueWith(t => { isPreloading = false; });
                }

                var postsToDisplay = cachedPosts.Skip(start).Take(settings.RepliesPerTopic).ToList();
                DisplayPosts(postsToDisplay, start);

                int totalPages = (int)Math.Ceiling(totalPosts / (double)settings.RepliesPerTopic);
                Console.WriteLine($"共 {totalPosts} 个回复，当前第 {page + 1} 页，共 {totalPages} 页");
                Console.WriteLine(new string('=', Console.WindowWidth));
                Console.WriteLine("输入b返回，n下一页，p上一页，j跳转，r刷新，w回复，l点赞，k收藏");
                var keyInfo = Console.ReadKey(true);
                char command = char.ToLower(keyInfo.KeyChar);
                switch (command)
                {
                    case 'b':
                        if (returnToSearch && lastSearchTerm != null)
                        {
                            exit = true;
                            await ShowSearchResults(lastSearchTerm, lastSearchPage, lastSearchWasAi);
                            return;
                        }
                        exit = true;
                        break;
                    case 'n':
                        if ((page + 1) * settings.RepliesPerTopic < cachedPosts.Count)
                        {
                            page++;
                        }
                        else
                        {
                            bool additionalPostsLoaded = await LoadPosts(topic.Id, cachedPosts, 50);
                            if (additionalPostsLoaded)
                            {
                                page++;
                            }
                            else
                            {
                                Console.WriteLine("\n已经是最后一页。");
                                Console.ReadKey();
                            }
                        }
                        break;
                    case 'p':
                        if (page > 0)
                        {
                            page--;
                        }
                        else
                        {
                            Console.WriteLine("已经是第一页，按任意键继续...");
                            Console.ReadKey();
                        }
                        break;
                    case 'r':
                        page = 0;
                        cachedPosts.Clear();
                        Console.WriteLine("正在刷新帖子内容...");
                        bool refreshed = await LoadPosts(topic.Id, cachedPosts, Math.Max(20, settings.RepliesPerTopic));
                        if (!refreshed)
                        {
                            Console.WriteLine("刷新失败，按任意键回...");
                            Console.ReadKey();
                            exit = true;
                        }
                        break;
                    case 'w':
                        await ReplyToTopic(topic.Id, postsToDisplay);
                        break;
                    case 'l':
                        await HandleReaction(topic.Id, postsToDisplay);
                        break;
                    case 'k':
                        await HandleBookmark(topic.Id, postsToDisplay);
                        break;
                    case 'j':
                        page = await HandleJump(topic.Id, cachedPosts, page, totalPosts);
                        break;
                    default:
                        Console.WriteLine("无效的命令，按任意键继续...");
                        Console.ReadKey();
                        break;
                }
            }
            returnToSearch = false;
        }

        private async Task PreloadNextBatch(int topicId, List<Post> cachedPosts)
        {
            try
            {
                await LoadPosts(topicId, cachedPosts, 50);
            }
            catch
            {
                await Task.Delay(3000);
                await LoadPosts(topicId, cachedPosts, 50);
            }
        }

        private async Task<bool> LoadPosts(int topicId, List<Post> cachedPosts, int limit)
        {
            try
            {
                string topicUrl = $"https://linux.do/t/topic/{topicId}.json";
                var topicResponse = await client.GetAsync(topicUrl);
                topicResponse.EnsureSuccessStatusCode();
                var topicContent = await topicResponse.Content.ReadAsStringAsync();
                var topicData = JsonSerializer.Deserialize<TopicResponse>(topicContent, _jsonOptions);
                var allPostIds = topicData?.PostStream?.Stream ?? new List<int>();
                var loadedPostIds = cachedPosts.Select(p => p.Id).ToHashSet();
                var unloadedPostIds = allPostIds.Where(id => !loadedPostIds.Contains(id)).Take(limit).ToList();

                if (unloadedPostIds.Count == 0)
                {
                    return false;
                }

                return await LoadPostsConcurrently(topicId, cachedPosts, unloadedPostIds);
            }
            catch
            {
                return false;
            }
        }

        private void DisplayPosts(List<Post> posts, int startIndex)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            int consoleWidth = Console.WindowWidth;
            string dividerLine = new string('-', consoleWidth);

            // 预计算最大宽度考虑中文字符
            int maxNoWidth = StringDisplayHelper.GetDisplayWidth((startIndex + posts.Count).ToString()) + 3;
            int maxIndexWidth = StringDisplayHelper.GetDisplayWidth(posts.Count.ToString()) + 1;
            int maxUserInfoWidth = posts.Max(p => 
            {
                string adminTag = p.Admin == true ? "[管理员] " : "";
                string userInfo = $"{adminTag}{p.Name}({p.Username})";
                return StringDisplayHelper.GetDisplayWidth(userInfo) > 40 ? 40 : StringDisplayHelper.GetDisplayWidth(userInfo);
            });
            int maxTimeWidth = 15;
            int maxLevelWidth = posts.Max(p => StringDisplayHelper.GetDisplayWidth($"用户等级{p.TrustLevel}") + 3);
            int maxReactionWidth = posts.Max(p => 
                StringDisplayHelper.GetDisplayWidth($"获赞{p.ReactionUsersCount ?? (p.Reactions?.Sum(r => r.Count) ?? 0)}")
            );

            foreach (var post in posts.Select((p, i) => new { Post = p, Index = i + 1 }))
            {
                string adminTag = post.Post.Admin == true ? "[管理员] " : "";
                string userInfo = $"{adminTag}{post.Post.Name}({post.Post.Username})";
                if (StringDisplayHelper.GetDisplayWidth(userInfo) > 40)
                {
                    userInfo = StringDisplayHelper.TruncateString(userInfo, 37) + "...";
                }

                string timeStr = GetTimeDifference(post.Post.CreatedAt);
                int totalReactions = post.Post.ReactionUsersCount ?? 
                    (post.Post.Reactions?.Sum(r => r.Count) ?? 0);
                
                string levelStr = $"用户等级{post.Post.TrustLevel}";
                string reactionStr = $"获赞{totalReactions}";
                
                Console.WriteLine(dividerLine);
                Console.WriteLine("{0} {1} {2} {3} {4} {5}",
                    StringDisplayHelper.PadRightWithWidth($"No.{startIndex + post.Index}", maxNoWidth),
                    StringDisplayHelper.PadRightWithWidth($"#{post.Index}", maxIndexWidth),
                    StringDisplayHelper.PadRightWithWidth(userInfo, maxUserInfoWidth),
                    StringDisplayHelper.PadRightWithWidth(timeStr, maxTimeWidth),
                    StringDisplayHelper.PadRightWithWidth(levelStr, maxLevelWidth),
                    reactionStr
                );

                string content = StripHtml(post.Post.Cooked ?? "");
                Console.WriteLine(content);
            }
            Console.WriteLine(dividerLine);
        }

        private string StripHtml(string input)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(input);
            string text = doc.DocumentNode.InnerText;

            return Regex.Replace(text, @"^\s*$\n|\r", string.Empty, RegexOptions.Multiline);
        }

        private void SettingsMenu()
        {
            bool exitSettings = false;
            while (!exitSettings)
            {
                Console.Clear();
                Console.WriteLine("设置：");
                Console.WriteLine($"1. 主页显示主题数（当前：{settings.TopicsPerPage}）");
                Console.WriteLine($"2. 每个主题显示回复数（当前：{settings.RepliesPerTopic}）");
                Console.WriteLine($"3. 设置Cookie（当前：{(string.IsNullOrEmpty(settings.Cookie) ? "未设置" : "已设置")})");
                Console.WriteLine("0. 返回");
                Console.Write("请选择要修改的设置项：");
                var keyInfo = Console.ReadKey(true);
                char choice = keyInfo.KeyChar;
                switch (choice)
                {
                    case '1':
                        Console.Write("请输入主页显示主题数：");
                        if (int.TryParse(Console.ReadLine(), out int topicsPerPage) && topicsPerPage > 0)
                        {
                            settings.TopicsPerPage = topicsPerPage;
                            loadedTopicPages.Clear();
                            currentTopicPage = 0;
                        }
                        else
                        {
                            Console.WriteLine("输入无效，请输入正整数。");
                            Console.ReadKey();
                        }
                        break;
                    case '2':
                        Console.Write("请输入每个主题显示回复数（1-100）：");
                        if (int.TryParse(Console.ReadLine(), out int repliesPerTopic) && repliesPerTopic > 0 && repliesPerTopic <= 100)
                        {
                            settings.RepliesPerTopic = repliesPerTopic;
                        }
                        else
                        {
                            Console.WriteLine("输入无效，请输入1到100之间的数字。");
                            Console.ReadKey();
                        }
                        break;
                    case '3':
                        Console.Write("请输入Cookie：");
                        settings.Cookie = Console.ReadLine()?.Trim() ?? "";
                        client.DefaultRequestHeaders.Remove("Cookie");
                        if (!string.IsNullOrEmpty(settings.Cookie))
                        {
                            client.DefaultRequestHeaders.Add("Cookie", settings.Cookie);
                        }
                        break;
                    case '0':
                        exitSettings = true;
                        break;
                    default:
                        Console.WriteLine("无效的选择，按任意键继续...");
                        Console.ReadKey();
                        break;
                }
                SaveSettings();
            }
        }

        private async Task RefreshCurrentPage()
        {
            loadedTopicPages.RemoveAt(currentTopicPage);
            bool refreshed = await LoadTopicPage(currentTopicPage);
            if (!refreshed)
            {
                Console.WriteLine("刷新失败，按任意键继续...");
                Console.ReadKey();
            }
        }

        private async Task NextTopicPage()
        {
            int nextPage = currentTopicPage + 1;
            bool loaded = await LoadTopicPage(nextPage);
            if (loaded)
            {
                currentTopicPage = nextPage;
            }
            else
            {
                Console.WriteLine("已经是最后一页，按任意键继续...");
                Console.ReadKey();
            }
        }

        private void PreviousTopicPage()
        {
            if (currentTopicPage > 0)
            {
                currentTopicPage--;
            }
            else
            {
                Console.WriteLine("已经是第一页，按任意键继续...");
                Console.ReadKey();
            }
        }

        private AppSettings LoadSettings()
        {
            try
            {
                if (File.Exists("settings.json"))
                {
                    var content = File.ReadAllText("settings.json");
                    return JsonSerializer.Deserialize<AppSettings>(content, _jsonOptions) ?? new AppSettings();
                }
                else
                {
                    return new AppSettings();
                }
            }
            catch
            {
                return new AppSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                var content = JsonSerializer.Serialize(settings, _jsonOptions);
                File.WriteAllText("settings.json", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n保存设置失败：{ex.Message}");
                Console.ReadKey();
            }
        }

        private async Task<string?> GetCsrfToken(int topicId)
        {
            try
            {
                var requestMessage = new HttpRequestMessage(HttpMethod.Get, $"https://linux.do/t/topic/{topicId}");
                requestMessage.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");

                var response = await client.SendAsync(requestMessage);
                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                var metaTag = doc.DocumentNode.SelectSingleNode("//meta[@name='csrf-token']");
                return metaTag?.GetAttributeValue("content", null);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n获取CSRF Token失败：{ex.Message}");
                return null;
            }
        }

        private async Task ReplyToTopic(int topicId, List<Post> currentPosts)
        {
            if (string.IsNullOrEmpty(settings.Cookie))
            {
                Console.WriteLine("\n需要设置Cookie才能回复帖子。按任意键继续...");
                Console.ReadKey();
                return;
            }

            // Get CSRF Token
            var csrfToken = await GetCsrfToken(topicId);
            if (string.IsNullOrEmpty(csrfToken))
            {
                Console.WriteLine("\n无法获取获取CSRF Token，回复失败。按任意键继续...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\n请输入回复内容（直接回车发送，输入:q取消）：");
            string? content = Console.ReadLine();
            
            if (string.IsNullOrEmpty(content) || content == ":q")
            {
                return;
            }

            Console.WriteLine("是否要引用某个回复？(y/N)");
            var quoteChoice = Console.ReadKey(true);
            int? replyToPostNumber = null;

            if (char.ToLower(quoteChoice.KeyChar) == 'y')
            {
                Console.WriteLine("\n请输入要用的回复序号（1-{0}）：", currentPosts.Count);
                if (int.TryParse(Console.ReadLine(), out int quoteIndex) && 
                    quoteIndex >= 1 && quoteIndex <= currentPosts.Count)
                {
                    replyToPostNumber = quoteIndex;
                }
            }

            var random = new Random();
            var data = new Dictionary<string, string>
            {
                ["raw"] = content,
                ["unlist_topic"] = "false",
                ["category"] = "4",
                ["topic_id"] = topicId.ToString(),
                ["is_warning"] = "false",
                ["archetype"] = "regular",
                ["typing_duration_msecs"] = random.Next(5000, 15000).ToString(),
                ["composer_open_duration_msecs"] = random.Next(20000, 30000).ToString(),
                ["featured_link"] = "",
                ["shared_draft"] = "false",
                ["draft_key"] = $"topic_{topicId}",
                ["nested_post"] = "true"
            };

            if (replyToPostNumber.HasValue)
            {
                data["whisper"] = "false";
                data["reply_to_post_number"] = replyToPostNumber.Value.ToString();
            }

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://linux.do/posts")
                {
                    Content = new FormUrlEncodedContent(data)
                };
                request.Headers.Add("X-CSRF-Token", csrfToken);

                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("\n回复成功！按任意键继续...");
                }
                else
                {
                    Console.WriteLine($"\n回复失败：{response.StatusCode} {response.Content}。按任意键继续...");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n回复出错：{ex.Message}。按任意键继续...");
            }
            
            Console.ReadKey();
        }

        private async Task LoadCategories()
        {
            try
            {
                if (cachedAboutData != null && (DateTime.Now - lastAboutDataUpdate).TotalMinutes < ABOUT_CACHE_MINUTES)
                {
                    return;
                }

                var response = await client.GetAsync("https://linux.do/about.json");
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                cachedAboutData = JsonSerializer.Deserialize<AboutResponse>(content, _jsonOptions);
                lastAboutDataUpdate = DateTime.Now;

                categoryNames.Clear();
                if (cachedAboutData?.Categories != null)
                {
                    foreach (var category in cachedAboutData.Categories)
                    {
                        if (category.Id != 0 && !string.IsNullOrEmpty(category.Name))
                        {
                            categoryNames[category.Id] = category.Name;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载分类信息失败：{ex.Message}");
            }
        }

        public AboutResponse? GetAboutData()
        {
            if (cachedAboutData != null && (DateTime.Now - lastAboutDataUpdate).TotalMinutes < ABOUT_CACHE_MINUTES)
            {
                return cachedAboutData;
            }
            return null;
        }

        private async Task HandleReaction(int topicId, List<Post> currentPosts)
        {
            if (string.IsNullOrEmpty(settings.Cookie))
            {
                Console.WriteLine("\n要设置Cookie才能点赞。按任意键继续...");
                Console.ReadKey();
                return;
            }

            var csrfToken = await GetCsrfToken(topicId);
            if (string.IsNullOrEmpty(csrfToken))
            {
                Console.WriteLine("\n无法获取CSRF Token，点赞失败。按任意键继续...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\n请输入要点赞的楼层序号（1-{0}）：", currentPosts.Count);
            if (int.TryParse(Console.ReadLine(), out int index) && 
                index >= 1 && index <= currentPosts.Count)
            {
                var post = currentPosts[index - 1];
                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Put, 
                        $"https://linux.do/discourse-reactions/posts/{post.Id}/custom-reactions/heart/toggle.json");
                    request.Headers.Add("X-CSRF-Token", csrfToken);

                    var response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("\n点赞成功！按任意键继续...");
                    }
                    else
                    {
                        Console.WriteLine($"\n点赞失败：{response.StatusCode}。按任意键继续...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n点赞出错：{ex.Message}。按任意键继续...");
                }
                Console.ReadKey();
            }
        }

        private async Task HandleBookmark(int topicId, List<Post> currentPosts)
        {
            if (string.IsNullOrEmpty(settings.Cookie))
            {
                Console.WriteLine("\n需要设置Cookie才能收藏按任意键继续...");
                Console.ReadKey();
                return;
            }

            var csrfToken = await GetCsrfToken(topicId);
            if (string.IsNullOrEmpty(csrfToken))
            {
                Console.WriteLine("\n无法获取CSRF Token，收藏失败。按任意键继续...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("\n请输入要收藏的楼层序号（1-{0}）：", currentPosts.Count);
            if (int.TryParse(Console.ReadLine(), out int index) && 
                index >= 1 && index <= currentPosts.Count)
            {
                var post = currentPosts[index - 1];
                var data = new Dictionary<string, string>
                {
                    ["reminder_at"] = "",
                    ["auto_delete_preference"] = "3",
                    ["bookmarkable_id"] = post.Id.ToString(),
                    ["bookmarkable_type"] = "Post"
                };

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "https://linux.do/bookmarks.json")
                    {
                        Content = new FormUrlEncodedContent(data)
                    };
                    request.Headers.Add("X-CSRF-Token", csrfToken);

                    var response = await client.SendAsync(request);
                    if (response.IsSuccessStatusCode)
                    {
                        Console.WriteLine("\n收藏成功！按任意键继续...");
                    }
                    else
                    {
                        Console.WriteLine($"\n收藏失败：{response.StatusCode}。按任意键继续...");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\n收藏出错：{ex.Message}。按任意键继续...");
                }
                Console.ReadKey();
            }
        }

        private async Task<int> HandleJump(int topicId, List<Post> cachedPosts, int currentPage, int totalPosts)
        {
            Console.WriteLine("\n跳转选项：");
            Console.WriteLine("1. 跳转到指定页码");
            Console.WriteLine("2. 跳转到指定楼层");
            Console.WriteLine("3. 跳转到最后一页");
            Console.Write("请选择（1-3）：");

            var choice = Console.ReadKey(true);
            Console.WriteLine(choice.KeyChar);

            switch (choice.KeyChar)
            {
                case '1':
                    Console.Write("请输入页码：");
                    if (int.TryParse(Console.ReadLine(), out int pageNum) && pageNum > 0)
                    {
                        int targetPage = pageNum - 1;
                        int requiredPosts = (targetPage + 1) * settings.RepliesPerTopic;
                        
                        while (cachedPosts.Count < requiredPosts)
                        {
                            bool loaded = await LoadPosts(topicId, cachedPosts, 50);
                            if (!loaded) break;
                        }

                        if (targetPage * settings.RepliesPerTopic < cachedPosts.Count)
                        {
                            return targetPage;
                        }
                        else
                        {
                            Console.WriteLine("页码超出范围。按任意键继续...");
                            Console.ReadKey();
                        }
                    }
                    break;

                case '2':
                    Console.Write($"请输入楼层号（1-{totalPosts}）：");
                    if (int.TryParse(Console.ReadLine(), out int postNum) && postNum > 0 && postNum <= totalPosts)
                    {
                        int targetPage = (postNum - 1) / settings.RepliesPerTopic;
                        int requiredPosts = (targetPage + 1) * settings.RepliesPerTopic;

                        while (cachedPosts.Count < requiredPosts)
                        {
                            bool loaded = await LoadPosts(topicId, cachedPosts, 50);
                            if (!loaded) break;
                        }

                        if (targetPage * settings.RepliesPerTopic < cachedPosts.Count)
                        {
                            return targetPage;
                        }
                        else
                        {
                            Console.WriteLine("楼层号超出范围。按任意键继续...");
                            Console.ReadKey();
                        }
                    }
                    break;

                case '3':
                    while (await LoadPosts(topicId, cachedPosts, 50)) { }
                    if (cachedPosts.Count > 0)
                    {
                        return (cachedPosts.Count - 1) / settings.RepliesPerTopic;
                    }
                    break;
            }
            return currentPage;
        }

        private async Task<bool> LoadPostsConcurrently(int topicId, List<Post> cachedPosts, List<int> postIds)
        {
            try
            {
                var tasks = new List<Task<List<Post>>>();
                int batchSize = 50;
                
                for (int i = 0; i < postIds.Count; i += batchSize)
                {
                    var batchIds = postIds.Skip(i).Take(batchSize).ToList();
                    tasks.Add(Task.Run(async () =>
                    {
                        string postsUrl = $"https://linux.do/t/{topicId}/posts.json?";
                        postsUrl += string.Join("&", batchIds.Select(id => $"post_ids[]={id}"));
                        postsUrl += "&include_suggested=true";

                        var postsResponse = await client.GetAsync(postsUrl);
                        postsResponse.EnsureSuccessStatusCode();
                        var postsContent = await postsResponse.Content.ReadAsStringAsync();
                        var postsData = JsonSerializer.Deserialize<PostsResponse>(postsContent, _jsonOptions);
                        return postsData?.PostStream?.Posts ?? new List<Post>();
                    }));

                    if (tasks.Count >= 5) // 用户执行跳转操作时，限制 5 个并发请求
                    {
                        var completedPosts = await Task.WhenAll(tasks);
                        foreach (var posts in completedPosts)
                        {
                            var newPosts = posts.Where(p => !cachedPosts.Any(cp => cp.Id == p.Id)).ToList();
                            cachedPosts.AddRange(newPosts);
                        }
                        tasks.Clear();
                    }
                }

                if (tasks.Count > 0)
                {
                    var completedPosts = await Task.WhenAll(tasks);
                    foreach (var posts in completedPosts)
                    {
                        var newPosts = posts.Where(p => !cachedPosts.Any(cp => cp.Id == p.Id)).ToList();
                        cachedPosts.AddRange(newPosts);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task HandleSearch()
        {
            lastSearchTerm = null;
            lastSearchPage = 1;
            lastSearchWasAi = false;
            returnToSearch = false;

            if (string.IsNullOrEmpty(settings.Cookie))
            {
                Console.WriteLine("\n需要设置Cookie才能搜索。按任意键继续...");
                Console.ReadKey();
                return;
            }

            var recentSearches = await GetRecentSearches();
            if (recentSearches?.Count > 0)
            {
                Console.WriteLine("\n最近搜索记录：");
                for (int i = 0; i < recentSearches.Count; i++)
                {
                    Console.WriteLine($"{i + 1}. {recentSearches[i]}");
                }
                Console.Write("\n选择数字使用历史记录，或直接输入新的搜索内容：\n> ");
            }
            else
            {
                Console.Write("\n请输入搜索内容\n> ");
            }

            string searchTerm = "";
            var lastSuggestTime = DateTime.MinValue;
            const int debounceMs = 300;

            while (true)
            {
                var keyInfo = Console.ReadKey(true);
                
                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    if (string.IsNullOrWhiteSpace(searchTerm)) return;
                    
                    if (int.TryParse(searchTerm, out int index) && recentSearches?.Count >= index && index > 0)
                    {
                        searchTerm = recentSearches[index - 1];
                    }
                    
                    await ExecuteSearch(searchTerm);
                    break;
                }
                else if (keyInfo.Key == ConsoleKey.Escape)
                {
                    return;
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (searchTerm.Length > 0)
                    {
                        searchTerm = searchTerm[..^1];
                        Console.Write("\b \b");
                    }
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    searchTerm += keyInfo.KeyChar;
                    Console.Write(keyInfo.KeyChar);
                }

                if (searchTerm.Length >= 2 && (DateTime.Now - lastSuggestTime).TotalMilliseconds >= debounceMs)
                {
                    lastSuggestTime = DateTime.Now;
                    await ShowSearchSuggestions(searchTerm);
                }
            }
        }

        private async Task<List<string>?> GetRecentSearches()
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://linux.do/u/recent-searches");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<RecentSearchResponse>(content, _jsonOptions);
                return result?.RecentSearches;
            }
            catch
            {
                return null;
            }
        }

        private async Task ShowSearchSuggestions(string searchTerm)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://linux.do/search/query?term={Uri.EscapeDataString(searchTerm)}&type_filter=exclude_topics");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SearchResponse>(content, _jsonOptions);

                Console.WriteLine();
                Console.WriteLine(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, Console.CursorTop - 2);
                
                if (result?.Posts?.Count > 0)
                {
                    Console.WriteLine("\n相关楼层：");
                    foreach (var post in result.Posts.Take(3))
                    {
                        var blurb = post.Blurb?.Length > 50 ? post.Blurb[..50] + "..." : post.Blurb;
                        Console.WriteLine($"- {post.Name}: {blurb}");
                    }
                }

                if (result?.Users?.Count > 0)
                {
                    Console.WriteLine("\n相关用户：");
                    foreach (var user in result.Users.Take(3))
                    {
                        Console.WriteLine($"- {user.Name} (@{user.Username})");
                    }
                }
                Console.SetCursorPosition(searchTerm.Length, Console.CursorTop - (result?.Posts?.Count ?? 0) - (result?.Users?.Count ?? 0) - 2);
            }
            catch
            {
                // 忽略搜索建议的错误
            }
        }

        private async Task ExecuteSearch(string term)
        {
            try
            {
                await LoadCategories();
                bool isAiSearch = false;
                int currentPage = 1;
                returnToSearch = true;
                await ShowSearchResults(term, currentPage, isAiSearch);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n搜索失败：{ex.Message}");
                Console.ReadKey();
            }
        }

        private async Task ShowSearchResults(string term, int page, bool isAiSearch)
        {
            lastSearchTerm = term;
            lastSearchPage = page;
            lastSearchWasAi = isAiSearch;

            try
            {
                string url;
                if (isAiSearch)
                {
                    url = $"https://linux.do/discourse-ai/embeddings/semantic-search?q={Uri.EscapeDataString(term)}";
                    if (page > 1) url += $"&page={page}";
                }
                else if (page == 1)
                {
                    url = $"https://linux.do/search/query?term={Uri.EscapeDataString(term)}";
                }
                else
                {
                    url = $"https://linux.do/search?q={Uri.EscapeDataString(term)}&page={page}";
                }

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Add("Accept", "application/json, text/javascript, */*; q=0.01");
                var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<SearchResponse>(content, _jsonOptions);

                Console.Clear();
                Console.WriteLine($"搜索结果 - \"{term}\" {(isAiSearch ? "[AI搜索]" : "")} - 第{page}页");
                Console.WriteLine(new string('=', Console.WindowWidth));

                if (result?.Topics?.Count > 0 || result?.Posts?.Count > 0)
                {
                    var groupedResults = new Dictionary<int, (SearchTopic? Topic, List<SearchPost> Posts)>();
                    
                    if (result?.Topics != null)
                    {
                        foreach (var topic in result.Topics)
                        {
                            groupedResults[topic.Id] = (topic, new List<SearchPost>());
                        }
                    }

                    if (result?.Posts != null)
                    {
                        foreach (var post in result.Posts)
                        {
                            if (!groupedResults.ContainsKey(post.TopicId))
                            {
                                groupedResults[post.TopicId] = (null, new List<SearchPost>());
                            }
                            groupedResults[post.TopicId].Posts.Add(post);
                        }
                    }

                    int index = 1;
                    foreach (var group in groupedResults)
                    {
                        var (topic, posts) = group.Value;
                        if (topic != null)
                        {
                            var tags = topic.Tags?.Count > 0 ? string.Join(", ", topic.Tags) : null;
                            var timeDiff = DateTime.Now - topic.LastPostedAt.ToLocalTime();
                            var timeText = timeDiff.TotalMinutes < 1 ? "刚刚" :
                                          timeDiff.TotalMinutes < 60 ? $"{(int)timeDiff.TotalMinutes}分钟前" :
                                          timeDiff.TotalHours < 24 ? $"{(int)timeDiff.TotalHours}小时前" :
                                          $"{(int)timeDiff.TotalDays}天前";
                            string categoryText = "";
                            if (topic.CategoryId != 0 && categoryNames.TryGetValue(topic.CategoryId, out string? categoryName))
                            {
                                categoryText = $"[{categoryName}] ";
                            }

                            Console.WriteLine($"\n{index}. {(string.IsNullOrEmpty(categoryText) ? "" : $"{categoryText}")}{(string.IsNullOrEmpty(tags) ? "" : $"[{tags}] ")}{topic.Title} ({topic.PostsCount}回复) (最后回复于{timeText})");
                        }

                        if (posts.Count > 0)
                        {
                            foreach (var post in posts)
                            {
                                var blurb = post.Blurb != null ? Regex.Replace(post.Blurb, @"\[([^\]]+)\]\([^\)]+\)|\*\*|\*|`|#|>|\[|\]|\(|\)", "") : null;
                                var likeText = post.LikeCount > 0 ? $"({post.LikeCount}赞)" : "";
                                var timeDiff = DateTime.Now - post.CreatedAt.ToLocalTime();
                                var timeText = timeDiff.TotalMinutes < 1 ? "刚刚" :
                                              timeDiff.TotalMinutes < 60 ? $"{(int)timeDiff.TotalMinutes}分钟前" :
                                              timeDiff.TotalHours < 24 ? $"{(int)timeDiff.TotalHours}小时前" :
                                              $"{(int)timeDiff.TotalDays}天前";

                                Console.WriteLine($"    └─ {post.Name}({post.Username}): {blurb} {likeText} (发表于{timeText})");
                            }
                        }
                        
                        index++;
                    }

                    Console.WriteLine("\n操作选项：");
                    Console.WriteLine("输入数字查看对应帖子，n下一页，p上一页，a切换到" + (isAiSearch ? "普通" : "AI") + "搜索，q返回到主页");
                    Console.Write("> ");
                    while (true)
                    {
                        var command = Console.ReadLine()?.ToLower();
                        switch (command)
                        {
                            case "q":
                                return;
                            case "n":
                                await ShowSearchResults(term, page + 1, isAiSearch);
                                return;
                            case "p":
                                if (page > 1)
                                {
                                    await ShowSearchResults(term, page - 1, isAiSearch);
                                }
                                return;
                            case "a":
                                await ShowSearchResults(term, 1, !isAiSearch);
                                return;
                            default:
                                if (int.TryParse(command, out int selectedIndex) &&
                                    selectedIndex > 0 && selectedIndex <= result?.Topics?.Count)
                                {
                                    var selectedTopic = result?.Topics[selectedIndex - 1];
                                    await ViewTopic(new Topic 
                                    { 
                                        Id = selectedTopic?.Id ?? 0,
                                        Title = selectedTopic?.Title ?? ""
                                    });
                                    return;
                                }
                                break;
                        }
                    }
                }
                else
                {
                    Console.WriteLine("\n没有找到相关结果。");
                    Console.WriteLine("\n按任意键返回...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n加载搜索结果失败：{ex.Message}");
                Console.WriteLine("\n按任意键返回...");
                Console.ReadKey();
            }
        }
    }
}
