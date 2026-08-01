# Provisio

**A first-run setup wizard for Windows.** Pick a few kits, answer a few questions, review everything in one list — then Provisio installs it all in a single batch.

Fresh Windows installs mean an evening of hunting down installers, clicking through the same wizards, and re-doing the same registry tweaks. Provisio turns that into a questionnaire. It ships as a single self-contained `.exe` with no installer, no runtime prerequisites, and no account.

Nothing is ever installed until you confirm it on the review screen.

---

## What it does

**Kits** — curated categories (Windows Basics, AI, Gaming, Work, Coding, Browsers, Utilities, Creative, Privacy & Security). Each kit is a short wizard: a question, a list of choices, and optional follow-up pages that only appear based on your answers.

**Recommended Setup** — a 10-question interactive questionnaire covering how you use the machine (everyday use, privacy, AI, development, office, gaming, creative work, system tweaks). Provisio compiles the answers into one curated, cross-kit list of suggestions, each labelled with the answer that produced it. You uncheck what you don't want before anything is queued.

**Review before install** — everything queued is shown grouped by kit, with a dry-run preview of the exact commands that will run. Uncheck anything you changed your mind about.

**Batch install** — one item at a time with per-item status and a live log. A failure never stops the rest; failed items can be retried, and the whole log exported. An optional System Restore point is created first.

**Search** — filter every app and tweak across all kits and queue results directly.

**Profiles** — save a full multi-kit selection and re-apply it in one click on the next machine.

**Kit Editor** — build your own kits without touching JSON: pages, choices, install actions, branch conditions and dependencies, then export as a `.provisio-kit` file to share.

**Package updates** — detects installed software with a newer version available through winget and updates the ones you select. Optionally checks at launch, or installs updates silently in the background.

**Languages** — English, German, Spanish and French, auto-detected from the OS and switchable at runtime.

## Install actions

Every choice in a kit maps to one action:

| Type | What it does |
|------|--------------|
| `winget` | `winget install -e --id <target>`, silent, with an optional `source` (`winget` or `msstore`) |
| `download` | Downloads a URL and runs it with your silent-install arguments. `.exe`, `.msi`, `.msix`, `.msixbundle`, `.appx`, `.appxbundle` — MSIX/APPX bundles are detected automatically and installed with `Add-AppxPackage` |
| `msix` | An MSIX/APPX bundle by URL or local path |
| `shell` | A command line through `cmd.exe` — npm, pip, uv, scoop, choco, or any setup task |
| `script` | A PowerShell script body, for registry tweaks and system settings |
| `none` | Installs nothing; used for yes/no questions that unlock follow-up pages |

Downloaded installers are cached, so a second machine (or an offline one) can re-use them.

## Kit format

Kits are plain JSON, so they can be read, diffed and shared:

```json
{
  "id": "example",
  "name": "Example Kit",
  "icon": "",
  "description": "Shown on the kit card.",
  "pages": [
    {
      "id": "local",
      "question": "Do you want to host AI locally?",
      "multiSelect": false,
      "choices": [
        { "id": "yes", "name": "Yes", "action": { "type": "none" } },
        { "id": "no",  "name": "No",  "action": { "type": "none" } }
      ]
    },
    {
      "id": "local-tools",
      "question": "Choose your local AI tool(s)",
      "multiSelect": true,
      "conditionPageId": "local",
      "conditionChoiceId": "yes",
      "choices": [
        {
          "id": "ollama",
          "name": "Ollama",
          "description": "Run LLMs from the command line",
          "action": { "type": "winget", "target": "Ollama.Ollama" }
        },
        {
          "id": "llama32",
          "name": "Llama 3.2",
          "dependsOn": ["ollama"],
          "action": { "type": "shell", "target": "ollama pull llama3.2" }
        }
      ]
    }
  ]
}
```

`conditionPageId` / `conditionChoiceId` make a page appear only when a specific answer was given. `dependsOn` orders the install queue so prerequisites run first.

The nine built-in kits are embedded in the executable. Dropping an `Assets\Kits\*.json` file next to the exe overrides a built-in kit with the same `id`.

## A note on trust

Kits are not just lists of apps — they can run shell commands, PowerShell scripts and downloaded installers under your account, and as administrator if the kit asks for it. Provisio warns before your first import and always shows an imported kit in the Kit Editor before anything runs. **Read every action in a kit you didn't write.**

## Where things are stored

```
%LOCALAPPDATA%\Provisio\
  settings.json    theme, language, update options
  Kits\            imported and custom kits (*.provisio-kit)
  Profiles\        saved selections
  Cache\           downloaded installers
```

Nothing is written to the registry, and uninstalling is deleting the `.exe` and that folder.

## Requirements

- Windows 10 1809 (17763) or newer, x64
- [winget](https://learn.microsoft.com/windows/package-manager/winget/) (App Installer) for `winget` actions and package updates
- Administrator rights only for the actions that say so — Provisio offers to relaunch elevated when needed

## Building from source

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet publish Provisio.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```

The result is one `Provisio.exe` in `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\` with no side files.

Built with WinUI 3 / Windows App SDK, unpackaged and self-contained.

## Project layout

```
Models\      kit and install-queue types
Services\    kit loading, install engine, winget updates, settings, localization, recommendations
Pages\       one page per screen (kit selection, wizard, summary, install, search, profiles, editor, updates, settings, recommended)
State\       pending selections and install-queue construction
Assets\Kits\ the built-in kit definitions
```

Adding a language is one dictionary in `Services\Loc.cs` plus one entry in `Loc.Languages`; XAML strings are tagged with `svc:L.Key="…"` and re-applied when the language changes.

