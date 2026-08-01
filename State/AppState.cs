using Provisio.Models;
using Provisio.Services;

namespace Provisio.State;

/// <summary>
/// Holds the user's pending selections across kits. Nothing here installs anything;
/// selections are only turned into an install queue on the Summary page.
/// </summary>
public static class AppState
{
    /// <summary>Kits selected on the landing screen, in wizard order.</summary>
    public static List<Kit> ActiveKits { get; } = new();

    /// <summary>Selections: kitId -> pageId -> choice ids.</summary>
    public static Dictionary<string, Dictionary<string, HashSet<string>>> Selections { get; } = new();

    public static void Reset()
    {
        ActiveKits.Clear();
        Selections.Clear();
    }

    public static HashSet<string> GetSelection(string kitId, string pageId)
    {
        if (!Selections.TryGetValue(kitId, out var pages))
        {
            pages = new Dictionary<string, HashSet<string>>();
            Selections[kitId] = pages;
        }
        if (!pages.TryGetValue(pageId, out var set))
        {
            set = new HashSet<string>();
            pages[pageId] = set;
        }
        return set;
    }

    public static void SetSelection(string kitId, string pageId, IEnumerable<string> choiceIds)
    {
        var set = GetSelection(kitId, pageId);
        set.Clear();
        foreach (var id in choiceIds) set.Add(id);
    }

    public static void ClearSelection(string kitId, string pageId) => GetSelection(kitId, pageId).Clear();

    public static bool IsSelected(string kitId, string pageId, string choiceId)
        => GetSelection(kitId, pageId).Contains(choiceId);

    /// <summary>Directly queue a choice (used by Search). Ensures its kit is active.</summary>
    public static void QueueChoice(Kit kit, string pageId, string choiceId)
    {
        if (!ActiveKits.Any(k => k.Id == kit.Id)) ActiveKits.Add(kit);
        GetSelection(kit.Id, pageId).Add(choiceId);
    }

    /// <summary>Pages of a kit that should actually be shown (conditions evaluated).</summary>
    public static List<KitPage> VisiblePages(Kit kit)
    {
        var result = new List<KitPage>();
        foreach (var page in kit.Pages)
        {
            if (!string.IsNullOrEmpty(page.ConditionPageId) && !string.IsNullOrEmpty(page.ConditionChoiceId))
            {
                if (!IsSelected(kit.Id, page.ConditionPageId, page.ConditionChoiceId))
                    continue;
            }
            result.Add(page);
        }
        return result;
    }

    /// <summary>Turn all selections into an ordered install queue (dependencies first).</summary>
    public static List<InstallItem> BuildQueue()
    {
        var items = new List<InstallItem>();
        foreach (var kit in ActiveKits)
        {
            var selected = new List<(KitPage page, KitChoice choice)>();
            foreach (var page in kit.Pages)
            {
                var set = GetSelection(kit.Id, page.Id);
                foreach (var choice in page.Choices)
                {
                    if (set.Contains(choice.Id) && choice.Action.Type != "none")
                        selected.Add((page, choice));
                }
            }

            // Order: choices with no dependencies first, then dependents (single pass, kits are curated acyclic).
            var ordered = new List<(KitPage page, KitChoice choice)>();
            var pending = new List<(KitPage page, KitChoice choice)>(selected);
            var placed = new HashSet<string>();
            for (int guard = 0; guard < 10 && pending.Count > 0; guard++)
            {
                var progressed = false;
                foreach (var entry in pending.ToList())
                {
                    if (entry.choice.DependsOn.All(d =>
                            placed.Contains(d) ||
                            !selected.Any(s => s.choice.Id == d)))
                    {
                        ordered.Add(entry);
                        placed.Add(entry.choice.Id);
                        pending.Remove(entry);
                        progressed = true;
                    }
                }
                if (!progressed) { ordered.AddRange(pending); break; } // cycle: just append
            }

            foreach (var (page, choice) in ordered)
            {
                items.Add(new InstallItem
                {
                    KitName = kit.Name,
                    ChoiceId = choice.Id,
                    Name = choice.Name,
                    Description = choice.Description,
                    Action = choice.Action
                });
            }
        }
        return items;
    }

    /// <summary>Exact commands that would run, for dry-run / preview mode.</summary>
    public static List<string> PreviewCommands()
        => BuildQueue().Select(i => $"{i.Name}:  {InstallEngine.DescribeCommand(i.Action)}").ToList();
}
