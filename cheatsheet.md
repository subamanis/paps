# Cheatsheet

## Navigation

| Command | Does |
|---|---|
| `z <name>` | Jump to a directory you have visited |
| `zi` | Pick interactively from zoxide's directories |
| `cdd [query]` | Fuzzy pick a directory and go there |
| `ll` | Long listing |
| `la` | Long listing with hidden files |
| `lt` | Directory tree, 2 levels |

```
z myproject
cdd
cdd docs
```

## Search

| Command | Does |
|---|---|
| `rg <text>` | Search text inside files |
| `fd <name>` | Search files and directories |
| `fzf` | Interactive fuzzy pick from a list |

```
rg "TODO" .
fd config
```

## Files

| Command | Does |
|---|---|
| `bat <file>` | View a file with syntax highlighting and line numbers |
| `open [query]` | Fuzzy pick a file and open it with the default app |
| `code <file>` | Open a file in VS Code |

```
open
open readme
bat src/main.rs
```

## Shortcuts

| Key | Does |
|---|---|
| `Ctrl+R` | Fuzzy search history |
| `Ctrl+T` | Fuzzy pick a file, paste it on the line |
| `Alt+C` | Fuzzy pick a directory, go there |
| `RightArrow` | Accept the suggestion shown |
| `F2` | Toggle ListView and InlineView |

## Predictions

The list under the line comes from two sources, each named on the right.

| Source | Gives |
|---|---|
| `ContextHistory` | History, filtered so it never offers another project's paths |
| `Completion` | What is in the current directory, via tab completion |

`Completion` stays quiet on the **first** word of a line. Type the verb first:

```
cd targ      works
cat pyr      works
pyr          nothing
.\pyr        works with Tab
```

## History

Cleans itself in two places.

**On typing.** A command that does not exist is never recorded. `cagro build` runs and is forgotten.

**On startup.** Past 1 MB with no other shell running, the file is trimmed: everything run twice or more, plus the last 1000 lines.

Lines that look like they carry credentials stay in session memory and never hit disk.

## Quick reference

```
z            jump to a known directory
cdd [query]  fuzzy pick a directory
ll / la      directory listing
lt           directory tree
rg           search text
fd           search files
bat          view files
open [query] fuzzy pick and open a file
fzf          fuzzy pick
```
