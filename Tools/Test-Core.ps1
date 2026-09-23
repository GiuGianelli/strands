$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$gameSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot "Assets/Scripts/Game") -Filter *.cs | ForEach-Object { $_.FullName })
Add-Type -Path $gameSources
$catalog = Get-Content -LiteralPath (Join-Path $projectRoot 'Assets/Resources/cards.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$definitions = @($catalog.cards | ForEach-Object {
    $definition = New-Object Strands.Game.CardDefinition
    foreach ($property in $_.PSObject.Properties) { $definition.($property.Name) = $property.Value }
    $definition
})
[Strands.Game.GameChecks]::Run([Strands.Game.CardDefinition[]]$definitions)
