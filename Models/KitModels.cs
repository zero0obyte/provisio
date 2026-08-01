using System.Text.Json.Serialization;

namespace Provisio.Models;

/// <summary>A curated category of setup steps, made of wizard pages.</summary>
public class Kit
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("icon")] public string Icon { get; set; } = ""; // Segoe Fluent Icons glyph (default)
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("builtin")] public bool Builtin { get; set; }
    [JsonPropertyName("pages")] public List<KitPage> Pages { get; set; } = new();
}

/// <summary>One wizard page: a question with a list of choices.</summary>
public class KitPage
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("question")] public string Question { get; set; } = "";
    [JsonPropertyName("multiSelect")] public bool MultiSelect { get; set; } = true;
    [JsonPropertyName("choices")] public List<KitChoice> Choices { get; set; } = new();

    /// <summary>Optional branching: only show this page if the given page had the given choice selected.</summary>
    [JsonPropertyName("conditionPageId")] public string? ConditionPageId { get; set; }
    [JsonPropertyName("conditionChoiceId")] public string? ConditionChoiceId { get; set; }
}

/// <summary>A single selectable option mapped to an install action.</summary>
public class KitChoice
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("action")] public InstallAction Action { get; set; } = new();

    /// <summary>Ids of other choices (same kit) that must be installed before this one.</summary>
    [JsonPropertyName("dependsOn")] public List<string> DependsOn { get; set; } = new();
}

/// <summary>What to run when a choice is queued: winget id, download URL+args, MSIX bundle, shell command or script.</summary>
public class InstallAction
{
    /// <summary>"none" | "winget" | "download" | "msix" | "shell" | "script"</summary>
    [JsonPropertyName("type")] public string Type { get; set; } = "none";
    /// <summary>Winget package id, download/MSIX URL, shell command line, or the script body.</summary>
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    /// <summary>Silent-install args for downloaded installers, or extra args appended to a shell command.</summary>
    [JsonPropertyName("args")] public string Args { get; set; } = "";
    /// <summary>Optional winget source ("winget" or "msstore"). Empty = winget's default resolution.</summary>
    [JsonPropertyName("source")] public string Source { get; set; } = "";
    /// <summary>True if this action needs elevation (set automatically for scripts too).</summary>
    [JsonPropertyName("requiresAdmin")] public bool RequiresAdmin { get; set; }
}
