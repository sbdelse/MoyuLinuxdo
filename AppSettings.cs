namespace MoyuLinuxdo
{
    public class AppSettings
    {
        public int TopicsPerPage { get; set; } = 10;

        private int repliesPerTopic = 5;
        public int RepliesPerTopic
        {
            get => repliesPerTopic;
            set => repliesPerTopic = (value > 100) ? 100 : (value < 1 ? 1 : value);
        }

        public string Cookie { get; set; } = "";
    }
}
