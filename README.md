# paps

PowerShell 7 setup: tools, profile, and a PSReadLine predictor that filters history by the directory you are in.

## Install

```powershell
git clone https://github.com/subamanis/paps.git D:\dev\Powershell\paps
D:\dev\Powershell\paps\setup.ps1
```

Idempotent. Skips whatever is already there.

| Flag | Effect |
|---|---|
| `-SkipTools` | Leave winget alone, do modules and profile only |
| `-CopyProfile` | Copy the profile instead of symlinking it |

Symlinks need Developer Mode or one elevated run. Without either, setup copies and says so.

## What it installs

winget: `zoxide` `fzf` `fd` `eza` `ripgrep` `bat`
Gallery: `CompletionPredictor` `Terminal-Icons`
Built here: `ContextHistoryPredictor`

## ContextHistoryPredictor

PSReadLine's own history ignores where you are. In project A you type `.\ta` and it offers `.\target\release\b.exe` from project B, which cannot even run there.

This predictor drops those. Per candidate line, per token:

| Token | Verdict | Result |
|---|---|---|
| `.\target\release\a.exe` | path, exists here | keep, rank first |
| `.\target\release\b.exe` | path, missing here | drop |
| `target/release/b.exe` | letter extension, missing | drop |
| `target/debug/deps` | `target` exists, full path does not | drop |
| `cargo build --release` | no path | keep, valid anywhere |
| `feature/new-thing` | no extension, no resolving ancestor | keep, may be a git branch |
| `git@github.com:me/repo.git` | has `@`, SSH URL | keep |

A token counts as a path when it has a backslash, is rooted, starts with `./`, ends in a letters-only extension, or has an ancestor that resolves as a directory here. Only then does its absence drop the line. Tokens containing `@` are skipped: SSH URLs and PowerShell `@(...)` literals.

Matching finds your text anywhere in the line, same as PSReadLine's built-in source. Lines matching from the start rank first. Quoted paths with spaces read as one token. Backtick continuations join into one line.

Location reaches the predictor through `LocationChangedAction`, since the process working directory does not follow `Set-Location` and the predictor runs off-thread.

Targets `net8.0` though PowerShell 7.6 runs on .NET 10, so no .NET 10 SDK needed.

Measured 0.08 ms per keystroke over 4334 history lines, worst case 1.1 ms. PSReadLine allows plugin predictors 20 ms.

## scripts/trim-history.ps1

PSReadLine never trims `ConsoleHost_history.txt`. This does, called from the profile.

Keeps every line run twice or more, plus the last 1000. Duplicates stay: they are the frequency count.

Two guards: does nothing under 1 MB, does nothing while another shell is running. The second matters because PSReadLine seeks to a stored file size to read what other windows wrote, and a shrinking file lands that seek past the end.

## History hygiene

`AddToHistoryHandler` runs before execution. It reads the command name off the AST and asks `Get-Command`. Missing command, the line is never recorded. `cagro build` runs and is forgotten.

Built on `GetDefaultAddToHistoryOption`, so PSReadLine's own secret filter stays live: `$token = "ghp_..."` becomes `MemoryOnly`.

That filter looks for `password|asplaintext|token|apikey|secret` in assignment or parameter position, so `curl -H "Authorization: Bearer ..."` slips past. A second pass catches credential shapes anywhere in the line: `Bearer`, `ghp_`, `github_pat_`, `sk-`, `xox[abprs]-`, `AKIA`, `AIza`, `glpat-`, and headers like `X-Api-Key:`. Zero false positives over 4334 real history lines.

Cost: 1 ms on a valid command, 20 ms on a typo.

## docs/cheatsheet.md

Commands and shortcuts.
