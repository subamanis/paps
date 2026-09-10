if (-not ('ContextHistoryPredictor.ContextHistory' -as [type])) {
    Import-Module (Join-Path $PSScriptRoot 'ContextHistoryPredictor.dll')
}

$option = Get-PSReadLineOption -ErrorAction Ignore

if ($option) {
    [ContextHistoryPredictor.ContextHistory]::HistoryPath = $option.HistorySavePath
}

if ($PWD.Provider.Name -eq 'FileSystem') {
    [ContextHistoryPredictor.ContextHistory]::Location = $PWD.ProviderPath
}

$previous = $ExecutionContext.SessionState.InvokeCommand.LocationChangedAction

$ExecutionContext.SessionState.InvokeCommand.LocationChangedAction = {
    param($source, $changed)

    if ($changed.NewPath.Provider.Name -eq 'FileSystem') {
        [ContextHistoryPredictor.ContextHistory]::Location = $changed.NewPath.ProviderPath
    }

    if ($previous) {
        & $previous $source $changed
    }
}.GetNewClosure()
