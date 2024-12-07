namespace MoyuLinuxdo.Helpers
{
    public static class StringDisplayHelper
    {
        /// <summary>
        /// 计算字符串在控制台中的显示宽度
        /// </summary>
        public static int GetDisplayWidth(string str)
        {
            int width = 0;
            foreach (char c in str)
            {
                width += IsFullWidth(c) ? 2 : 1;
            }
            return width;
        }

        /// <summary>
        /// 判断字符是否是全角字符
        /// </summary>
        public static bool IsFullWidth(char c)
        {
            return (c >= 0x4E00 && c <= 0x9FFF) ||      // CJK统一汉字
                   (c >= 0x3000 && c <= 0x303F) ||      // CJK标点符号
                   (c >= 0xFF00 && c <= 0xFFEF);        // 全角ASCII、全角标点
        }

        /// <summary>
        /// 按显示宽度进行右填充
        /// </summary>
        public static string PadRightWithWidth(string str, int width)
        {
            int currentWidth = GetDisplayWidth(str);
            return str + new string(' ', width - currentWidth);
        }

        /// <summary>
        /// 按显示宽度截断字符串
        /// </summary>
        public static string TruncateString(string str, int maxWidth)
        {
            int width = 0;
            int i = 0;
            for (; i < str.Length; i++)
            {
                int charWidth = IsFullWidth(str[i]) ? 2 : 1;
                if (width + charWidth > maxWidth)
                    break;
                width += charWidth;
            }
            return str.Substring(0, i);
        }
    }
} 