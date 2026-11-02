using System.Globalization;
using System.Text.RegularExpressions;

namespace DesktopTodo.Core;

public sealed record PlannedLine(DateOnly Date, string Text);

public static class PlanPaste
{
    // Explicit dates can span months and years; month/day headings use the selected year's calendar.
    public static IReadOnlyList<PlannedLine> Parse(string input, DateOnly fallback, DateOnly today)
    {
        var result = new List<PlannedLine>();
        DateOnly date = fallback;
        int number = 0;
        foreach (string raw in input.Replace("\r", "").Split('\n'))
        {
            number++;
            string line = raw.Trim();
            if (line.Length == 0) continue;
            string heading = line.TrimStart('#').Trim().TrimEnd(':', '：');
            if (heading is "今天" or "明天" or "后天")
            { date = today.AddDays(heading == "今天" ? 0 : heading == "明天" ? 1 : 2); continue; }
            var match = Regex.Match(heading, @"^(?:(?<y>\d{4})[-/年])?(?<m>\d{1,2})[-/月](?<d>\d{1,2})日?(?:\s+(?:星期|周)[一二三四五六日天])?$");
            if (match.Success)
            {
                try { date = new DateOnly(match.Groups["y"].Success ? int.Parse(match.Groups["y"].Value) : fallback.Year,
                    int.Parse(match.Groups["m"].Value), int.Parse(match.Groups["d"].Value)); }
                catch (ArgumentOutOfRangeException) { throw new ArgumentException($"第 {number} 行日期无效：{heading}"); }
                continue;
            }
            if (Regex.IsMatch(heading, @"^\d{4}[-/年]\d"))
                throw new ArgumentException($"第 {number} 行日期格式无效。日期请单独写一行，例如 2026-11-01。");
            string text = Regex.Replace(line, @"^(?:\d+[.、)）]\s*|[-*•]\s+)", "").Trim();
            if (text.Length > 0) result.Add(new(date, text));
        }
        if (result.Count == 0) throw new ArgumentException("没有可导入的任务。");
        return result;
    }
}
