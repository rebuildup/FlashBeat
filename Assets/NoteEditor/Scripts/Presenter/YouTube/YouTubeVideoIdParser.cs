using System.Text.RegularExpressions;

namespace NoteEditor.Presenter.YouTube
{
    public static class YouTubeVideoIdParser
    {
        private static readonly Regex Pattern = new Regex(
            @"(?:v=|youtu\.be/|^)([A-Za-z0-9_-]{11})",
            RegexOptions.Compiled
        );

        public static bool TryParse(string input, out string videoId)
        {
            videoId = null;
            if (string.IsNullOrWhiteSpace(input)) return false;
            var match = Pattern.Match(input.Trim());
            if (!match.Success) return false;
            videoId = match.Groups[1].Value;
            return true;
        }
    }
}
