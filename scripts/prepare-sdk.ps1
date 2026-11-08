param([string]$SdkDirectory = 'C:\Program Files\Ashboard Vision\sdk')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$feed = Join-Path $root '.sdk-feed'
New-Item -ItemType Directory -Force -Path $feed | Out-Null
foreach ($name in @('AshboardVision.Sdk','AshboardVision.Sdk.Testing','AshboardVision.Sdk.Tool','AshboardVision.Sdk.Templates')) {
    $source = Join-Path $SdkDirectory ($name + '.1.0.0.nupkg')
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Paquet officiel absent : $source" }
    Copy-Item -LiteralPath $source -Destination $feed -Force
}
Write-Output "SDK officiel 1.0.0 disponible dans $feed"
