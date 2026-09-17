using System.Text.RegularExpressions;

namespace TopSolid.Automation.AI.Studio.Chat;

internal static class PdmListRequest
{
    internal sealed record Request(string[] Tools, string? Order, bool Dates);
    public static string[] Tools(string text) => Parse(text)?.Tools ?? [];
    // Explicit full-list intent only. Filters, comparisons, negation and changes
    // retain the general model workflow. Recognize the spelling in the reported log.
    public static Request? Parse(string text)
    {
        var words = Regex.Matches(text.ToLowerInvariant().Replace("'s", ""), @"[\p{L}\p{N}]+").Select(m => m.Value).ToArray();
        var allowed = new HashSet<string>("please can you could would list show get give me the of all every topsolid pdm project projects library libraries name names and with friendly order ordered sort sorted by creation cretaion created date dates oldest newest old new first to from ascending descending alphabetical alphabetically alphabetic reverse a z 프로젝트 프로젝트와 라이브러리 이름 이름을 전체 모든 목록 목록을 보여줘 알려줘 생성일 날짜 오래된 순으로 최신순으로 이름순 가나다순 알파벳순".Split(' '));
        if (words.Length == 0 || words.Any(w => !allowed.Contains(w)) ||
            !words.Any(w => w is "list" or "show" or "get" or "give" or "목록" or "목록을" or "보여줘" or "알려줘")) return null;
        var names = new List<string>();
        if (words.Any(w => w is "project" or "projects" or "프로젝트" or "프로젝트와")) names.Add("topsolid_list_projects");
        if (words.Any(w => w is "library" or "libraries" or "라이브러리")) names.Add("topsolid_list_libraries");
        var normalized = string.Join(" ", words);
        var dates = words.Any(w => w is "date" or "dates" or "creation" or "cretaion" or "생성일" or "날짜");
        var alphabetical = words.Any(w => w is "alphabetical" or "alphabetically" or "alphabetic" or "이름순" or "가나다순" or "알파벳순") ||
            normalized.Contains("by name") || normalized.Contains("by friendly") || Regex.IsMatch(normalized, @"\b(a(?: to)? z|z(?: to)? a)\b");
        var ascending = words.Contains("ascending"); var descending = words.Contains("descending");
        if (alphabetical)
        {
            if (ascending && descending || words.Any(w => w is "oldest" or "old" or "newest" or "new" or "오래된" or "최신순으로")) return null;
            return names.Count == 0 ? null : new Request(names.ToArray(), descending || words.Contains("reverse") || Regex.IsMatch(normalized, @"\bz(?: to)? a\b") ? "nameDescending" : "nameAscending", dates);
        }
        if (words.Contains("reverse") || words.Any(w => w is "a" or "z") || (ascending || descending) && !dates) return null;
        var old = words.Any(w => w is "oldest" or "old" or "ascending" or "오래된");
        var recent = words.Any(w => w is "newest" or "new" or "descending" or "최신순으로");
        string? order = old && recent ? normalized.Contains("oldest to newest") ? "oldestFirst" : normalized.Contains("newest to oldest") ? "newestFirst" : null : old ? "oldestFirst" : recent ? "newestFirst" : null;
        if ((old && recent && order == null) || (order == null && words.Any(w => w is "order" or "ordered" or "sort" or "sorted"))) return null;
        return names.Count == 0 ? null : new Request(names.ToArray(), order, order != null || dates);
    }
}
