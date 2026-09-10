# ============================================
# PowerShell 7 Profile
# ============================================


# --------------------------------------------
# Command history / predictions
# --------------------------------------------

if ((Get-Item "$env:APPDATA\Microsoft\Windows\PowerShell\PSReadLine\ConsoleHost_history.txt" -ErrorAction Ignore).Length -ge 1MB) {
    & "$PSScriptRoot\Scripts\trim-history.ps1"
}

Import-Module CompletionPredictor
Import-Module ContextHistoryPredictor
Set-PSReadLineOption -PredictionSource Plugin
Set-PSReadLineOption -PredictionViewStyle ListView

Set-PSReadLineOption -AddToHistoryHandler {
    param($line)

    $default = [Microsoft.PowerShell.PSConsoleReadLine]::GetDefaultAddToHistoryOption($line)

    if ($default -ne [Microsoft.PowerShell.AddToHistoryOption]::MemoryAndFile) {
        return $default
    }

    try {
        $ast = [System.Management.Automation.Language.Parser]::ParseInput($line, [ref] $null, [ref] $null)
        $command = $ast.Find({ param($node) $node -is [System.Management.Automation.Language.CommandAst] }, $true)

        if ($null -eq $command) {
            return $default
        }

        $name = $command.GetCommandName()

        if ([string]::IsNullOrEmpty($name) -or [System.Management.Automation.WildcardPattern]::ContainsWildcardCharacters($name)) {
            return $default
        }

        if (Get-Command $name -ErrorAction Ignore) {
            return $default
        }

        return [Microsoft.PowerShell.AddToHistoryOption]::SkipAdding
    }
    catch {
        return $default
    }
}


# --------------------------------------------
# zoxide
# --------------------------------------------

Invoke-Expression (& { (zoxide init powershell | Out-String) })


# --------------------------------------------
# Directory listing
# --------------------------------------------

function ll {
    eza -l --git @args
}

function la {
    eza -la --git @args
}

function lt {
    eza --tree --level=2 @args
}


# --------------------------------------------
# Terminal icons
# --------------------------------------------

Import-Module Terminal-Icons

# --------------------------------------------
# fzf
# --------------------------------------------

# Ctrl+R: fuzzy history selection
Set-PSReadLineKeyHandler -Key Ctrl+r -ScriptBlock {
    $history = Get-Content (Get-PSReadLineOption).HistorySavePath |
        Select-Object -Unique

    $selected = $history | fzf --tac

    if ($selected) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($selected)
    }
}

# Ctrl+T: fuzzy file selection
Set-PSReadLineKeyHandler -Key Ctrl+t -ScriptBlock {
    $file = fd --type f |
        fzf

    if ($file) {
        [Microsoft.PowerShell.PSConsoleReadLine]::Insert($file)
    }
}


# Alt+C: fuzzy directory selection
Set-PSReadLineKeyHandler -Key Alt+c -ScriptBlock {
    $directory = fd --type d | fzf

    if ($directory) {
        Set-Location $directory
        [Microsoft.PowerShell.PSConsoleReadLine]::CancelLine()
    }
}


# --------------------------------------------
# custom shortcuts
# --------------------------------------------

function cdd {
    $query = $args -join ' '
    $dir = fd --type d | fzf --query $query

    if ($dir) {
        Set-Location $dir
    }
}

function open {
    $query = $args -join ' '
    $file = fd --type f | fzf --query $query

    if ($file) {
        Invoke-Item $file
    }
}