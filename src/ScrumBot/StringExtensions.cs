using System.Linq;

namespace ScrumBot
{
    public static class StringExtensions
    {
        public static string TrimStartOfEachLine(this string input, char trimChar)
        {
            var lines = input.Split('\n').Select(line => line.TrimStart(trimChar));

            return string.Join('\n', lines);
        }
    }
}
