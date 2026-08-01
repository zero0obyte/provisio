using Provisio.Models;

namespace Provisio.Services;

/// <summary>A pointer into a kit: which choice on which page of which kit.</summary>
public record ChoiceRef(string KitId, string PageId, string ChoiceId);

/// <summary>One answer option, with the kit choices it argues for.</summary>
public class RecOption
{
    public required string Id { get; init; }
    public required string TextKey { get; init; }
    public List<ChoiceRef> Recommends { get; init; } = new();
    public string Text => Loc.T(TextKey);
}

/// <summary>One questionnaire step.</summary>
public class RecQuestion
{
    public required string Id { get; init; }
    public required string TextKey { get; init; }
    public bool MultiSelect { get; init; } = true;
    public List<RecOption> Options { get; init; } = new();
    public string Text => Loc.T(TextKey);
}

/// <summary>A resolved suggestion: a real kit choice plus why it was suggested.</summary>
public class Recommendation
{
    public required Kit Kit { get; init; }
    public required KitPage Page { get; init; }
    public required KitChoice Choice { get; init; }
    public List<string> Reasons { get; } = new();
}

/// <summary>
/// The "Recommended Setup" questionnaire: a fixed set of multiple-choice questions
/// spanning the kit domains, each option mapping to concrete kit choices. Answers are
/// resolved against the currently loaded kits, so options pointing at a kit the user
/// deleted (or a custom kit that replaced a built-in one) are simply dropped.
/// </summary>
public static class RecommendationService
{
    private static ChoiceRef R(string kit, string page, string choice) => new(kit, page, choice);

    public static List<RecQuestion> Questions() => new()
    {
        new RecQuestion
        {
            Id = "use",
            TextKey = "rec.q.use",
            MultiSelect = true,
            Options =
            {
                new RecOption
                {
                    Id = "general", TextKey = "rec.q.use.general",
                    Recommends =
                    {
                        R("windows-basics", "essentials", "7zip"),
                        R("windows-basics", "essentials", "vlc"),
                        R("windows-basics", "essentials", "notepadpp"),
                        R("utilities", "utilities", "everything")
                    }
                },
                new RecOption
                {
                    Id = "work", TextKey = "rec.q.use.work",
                    Recommends =
                    {
                        R("work", "communication", "teams"),
                        R("work", "communication", "zoom"),
                        R("work", "productivity", "acrobat"),
                        R("utilities", "utilities", "sharex")
                    }
                },
                new RecOption
                {
                    Id = "dev", TextKey = "rec.q.use.dev",
                    Recommends =
                    {
                        R("coding", "editors", "vscode"),
                        R("coding", "toolchains", "git"),
                        R("coding", "terminal", "terminal"),
                        R("coding", "terminal", "pwsh")
                    }
                },
                new RecOption
                {
                    Id = "gaming", TextKey = "rec.q.use.gaming",
                    Recommends =
                    {
                        R("gaming", "runtimes", "vcredist"),
                        R("gaming", "runtimes", "directx"),
                        R("gaming", "runtimes", "dotnet"),
                        R("gaming", "tools", "discord")
                    }
                }
            }
        },

        new RecQuestion
        {
            Id = "level",
            TextKey = "rec.q.level",
            MultiSelect = false,
            Options =
            {
                new RecOption
                {
                    Id = "beginner", TextKey = "rec.q.level.beginner",
                    Recommends =
                    {
                        R("windows-basics", "essentials", "7zip"),
                        R("windows-basics", "tweaks", "fileext"),
                        R("privacy", "tools", "bitwarden")
                    }
                },
                new RecOption
                {
                    Id = "confident", TextKey = "rec.q.level.confident",
                    Recommends =
                    {
                        R("windows-basics", "essentials", "powertoys"),
                        R("windows-basics", "tweaks", "fileext"),
                        R("utilities", "utilities", "everything"),
                        R("utilities", "utilities", "wiztree")
                    }
                },
                new RecOption
                {
                    Id = "power", TextKey = "rec.q.level.power",
                    Recommends =
                    {
                        R("windows-basics", "essentials", "powertoys"),
                        R("windows-basics", "essentials", "terminal"),
                        R("windows-basics", "tweaks", "hiddenfiles"),
                        R("windows-basics", "tweaks", "long-paths"),
                        R("utilities", "utilities", "autohotkey"),
                        R("utilities", "utilities", "ditto"),
                        R("utilities", "utilities", "hwinfo")
                    }
                }
            }
        },

        new RecQuestion
        {
            Id = "browser",
            TextKey = "rec.q.browser",
            MultiSelect = true,
            Options =
            {
                new RecOption { Id = "chrome", TextKey = "rec.q.browser.chrome", Recommends = { R("browsers", "browsers", "chrome") } },
                new RecOption { Id = "firefox", TextKey = "rec.q.browser.firefox", Recommends = { R("browsers", "browsers", "firefox") } },
                new RecOption
                {
                    Id = "brave", TextKey = "rec.q.browser.brave",
                    Recommends =
                    {
                        R("browsers", "browsers", "brave"),
                        R("browsers", "browsers", "librewolf")
                    }
                },
                new RecOption { Id = "keep", TextKey = "rec.q.browser.keep" }
            }
        },

        new RecQuestion
        {
            Id = "privacy",
            TextKey = "rec.q.privacy",
            MultiSelect = false,
            Options =
            {
                new RecOption { Id = "default", TextKey = "rec.q.privacy.default" },
                new RecOption
                {
                    Id = "some", TextKey = "rec.q.privacy.some",
                    Recommends =
                    {
                        R("privacy", "tweaks", "ad-id"),
                        R("privacy", "tweaks", "tailored"),
                        R("windows-basics", "tweaks", "disable-suggestions"),
                        R("privacy", "tools", "bitwarden")
                    }
                },
                new RecOption
                {
                    Id = "max", TextKey = "rec.q.privacy.max",
                    Recommends =
                    {
                        R("privacy", "tools", "shutup10"),
                        R("privacy", "tools", "simplewall"),
                        R("privacy", "tools", "veracrypt"),
                        R("privacy", "tools", "protonvpn"),
                        R("privacy", "tools", "bitwarden"),
                        R("privacy", "tweaks", "ad-id"),
                        R("privacy", "tweaks", "activity-history"),
                        R("privacy", "tweaks", "tailored"),
                        R("windows-basics", "tweaks", "disable-telemetry"),
                        R("browsers", "browsers", "librewolf")
                    }
                }
            }
        },

        new RecQuestion
        {
            Id = "ai",
            TextKey = "rec.q.ai",
            MultiSelect = true,
            Options =
            {
                new RecOption { Id = "none", TextKey = "rec.q.ai.none" },
                new RecOption
                {
                    Id = "chat", TextKey = "rec.q.ai.chat",
                    Recommends =
                    {
                        R("ai", "clients", "claude"),
                        R("ai", "clients", "chatgpt")
                    }
                },
                new RecOption
                {
                    Id = "cli", TextKey = "rec.q.ai.cli",
                    Recommends =
                    {
                        R("ai", "cli", "nodejs"),
                        R("ai", "cli", "git"),
                        R("ai", "cli", "claude-code"),
                        R("ai", "cli", "gemini-cli"),
                        R("ai", "cli", "codex-cli")
                    }
                },
                new RecOption
                {
                    Id = "local", TextKey = "rec.q.ai.local",
                    Recommends =
                    {
                        R("ai", "local-tools", "ollama"),
                        R("ai", "local-tools", "lmstudio"),
                        R("ai", "local-models", "llama32")
                    }
                }
            }
        },

        new RecQuestion
        {
            Id = "dev",
            TextKey = "rec.q.dev",
            MultiSelect = true,
            Options =
            {
                new RecOption { Id = "none", TextKey = "rec.q.dev.none" },
                new RecOption
                {
                    Id = "web", TextKey = "rec.q.dev.web",
                    Recommends =
                    {
                        R("coding", "editors", "vscode"),
                        R("coding", "toolchains", "nodejs"),
                        R("coding", "toolchains", "git"),
                        R("coding", "terminal", "gh")
                    }
                },
                new RecOption
                {
                    Id = "python", TextKey = "rec.q.dev.python",
                    Recommends =
                    {
                        R("coding", "editors", "vscode"),
                        R("coding", "toolchains", "python"),
                        R("coding", "toolchains", "git"),
                        R("coding", "toolchains", "docker")
                    }
                },
                new RecOption
                {
                    Id = "dotnet", TextKey = "rec.q.dev.dotnet",
                    Recommends =
                    {
                        R("coding", "editors", "vs2022"),
                        R("coding", "toolchains", "dotnet-sdk"),
                        R("coding", "toolchains", "git")
                    }
                }
            }
        },

        new RecQuestion
        {
            Id = "office",
            TextKey = "rec.q.office",
            MultiSelect = true,
            Options =
            {
                new RecOption { Id = "none", TextKey = "rec.q.office.none" },
                new RecOption
                {
                    Id = "free", TextKey = "rec.q.office.free",
                    Recommends =
                    {
                        R("work", "office", "libreoffice"),
                        R("work", "productivity", "acrobat")
                    }
                },
                new RecOption { Id = "ms", TextKey = "rec.q.office.ms", Recommends = { R("work", "office", "m365") } },
                new RecOption
                {
                    Id = "notes", TextKey = "rec.q.office.notes",
                    Recommends =
                    {
                        R("work", "productivity", "obsidian"),
                        R("work", "productivity", "notion")
                    }
                }
            }
        },

        new RecQuestion
        {
            Id = "gaming",
            TextKey = "rec.q.gaming",
            MultiSelect = true,
            Options =
            {
                new RecOption { Id = "none", TextKey = "rec.q.gaming.none" },
                new RecOption
                {
                    Id = "casual", TextKey = "rec.q.gaming.casual",
                    Recommends =
                    {
                        R("gaming", "stores", "steam"),
                        R("gaming", "runtimes", "vcredist"),
                        R("gaming", "tools", "discord")
                    }
                },
                new RecOption
                {
                    Id = "serious", TextKey = "rec.q.gaming.serious",
                    Recommends =
                    {
                        R("gaming", "stores", "steam"),
                        R("gaming", "stores", "epic"),
                        R("gaming", "stores", "gog"),
                        R("gaming", "stores", "xbox"),
                        R("gaming", "runtimes", "vcredist"),
                        R("gaming", "runtimes", "directx"),
                        R("gaming", "runtimes", "dotnet"),
                        R("gaming", "tools", "afterburner"),
                        R("windows-basics", "tweaks", "ultimate-power")
                    }
                },
                new RecOption
                {
                    Id = "stream", TextKey = "rec.q.gaming.stream",
                    Recommends =
                    {
                        R("gaming", "tools", "obs"),
                        R("utilities", "utilities", "sharex"),
                        R("creative", "av", "audacity")
                    }
                }
            }
        },

        new RecQuestion
        {
            Id = "creative",
            TextKey = "rec.q.creative",
            MultiSelect = true,
            Options =
            {
                new RecOption { Id = "none", TextKey = "rec.q.creative.none" },
                new RecOption
                {
                    Id = "image", TextKey = "rec.q.creative.image",
                    Recommends =
                    {
                        R("creative", "image", "gimp"),
                        R("creative", "image", "krita"),
                        R("creative", "image", "inkscape")
                    }
                },
                new RecOption
                {
                    Id = "video", TextKey = "rec.q.creative.video",
                    Recommends =
                    {
                        R("creative", "av", "davinci"),
                        R("creative", "av", "kdenlive"),
                        R("creative", "av", "audacity"),
                        R("creative", "av", "handbrake")
                    }
                },
                new RecOption { Id = "3d", TextKey = "rec.q.creative.3d", Recommends = { R("creative", "3d", "blender") } }
            }
        },

        new RecQuestion
        {
            Id = "tweaks",
            TextKey = "rec.q.tweaks",
            MultiSelect = false,
            Options =
            {
                new RecOption { Id = "none", TextKey = "rec.q.tweaks.none" },
                new RecOption
                {
                    Id = "quality", TextKey = "rec.q.tweaks.quality",
                    Recommends =
                    {
                        R("windows-basics", "tweaks", "fileext"),
                        R("windows-basics", "tweaks", "hiddenfiles"),
                        R("windows-basics", "tweaks", "darkmode"),
                        R("windows-basics", "tweaks", "taskbar-left")
                    }
                },
                new RecOption
                {
                    Id = "debloat", TextKey = "rec.q.tweaks.debloat",
                    Recommends =
                    {
                        R("windows-basics", "tweaks", "fileext"),
                        R("windows-basics", "tweaks", "hiddenfiles"),
                        R("windows-basics", "tweaks", "darkmode"),
                        R("windows-basics", "tweaks", "disable-suggestions"),
                        R("windows-basics", "debloat", "remove-xbox"),
                        R("windows-basics", "debloat", "remove-cortana"),
                        R("windows-basics", "debloat", "remove-tips")
                    }
                }
            }
        }
    };

    /// <summary>
    /// Turn answers (questionId -> selected option ids) into a de-duplicated list of
    /// real kit choices, ordered by the kit order shown on the landing page.
    /// </summary>
    public static List<Recommendation> Build(
        IReadOnlyList<RecQuestion> questions,
        IReadOnlyDictionary<string, HashSet<string>> answers)
    {
        var byRef = new Dictionary<ChoiceRef, Recommendation>();

        foreach (var question in questions)
        {
            if (!answers.TryGetValue(question.Id, out var chosen)) continue;
            foreach (var option in question.Options)
            {
                if (!chosen.Contains(option.Id)) continue;
                foreach (var reference in option.Recommends)
                {
                    if (!byRef.TryGetValue(reference, out var rec))
                    {
                        var resolved = Resolve(reference);
                        if (resolved is null) continue;   // kit/page/choice no longer exists
                        rec = resolved;
                        byRef[reference] = rec;
                    }
                    var reason = option.Text;
                    if (!rec.Reasons.Contains(reason)) rec.Reasons.Add(reason);
                }
            }
        }

        var kitOrder = KitService.Kits.Select((k, i) => (k.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return byRef.Values
            .OrderBy(r => kitOrder.TryGetValue(r.Kit.Id, out var i) ? i : int.MaxValue)
            .ThenBy(r => r.Choice.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static Recommendation? Resolve(ChoiceRef reference)
    {
        var kit = KitService.Find(reference.KitId);
        var page = kit?.Pages.FirstOrDefault(p => p.Id == reference.PageId);
        var choice = page?.Choices.FirstOrDefault(c => c.Id == reference.ChoiceId);
        if (kit is null || page is null || choice is null || choice.Action.Type == "none") return null;
        return new Recommendation { Kit = kit, Page = page, Choice = choice };
    }

    /// <summary>
    /// Queue the accepted recommendations into the shared app state. Choices that live on a
    /// conditional page also get their unlocking answer selected, so the wizard shows the page
    /// as reachable instead of silently hiding an item that is already queued.
    /// </summary>
    public static int Queue(IEnumerable<Recommendation> accepted)
    {
        var count = 0;
        foreach (var rec in accepted)
        {
            State.AppState.QueueChoice(rec.Kit, rec.Page.Id, rec.Choice.Id);
            if (!string.IsNullOrEmpty(rec.Page.ConditionPageId) && !string.IsNullOrEmpty(rec.Page.ConditionChoiceId))
                State.AppState.GetSelection(rec.Kit.Id, rec.Page.ConditionPageId).Add(rec.Page.ConditionChoiceId);
            count++;
        }
        return count;
    }
}
