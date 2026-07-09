[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [switch]$SelfContained,

    [switch]$Zip
)

$ErrorActionPreference = "Stop"

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Project = Join-Path $Root "src\PureView\PureView.csproj"
$Output = Join-Path $Root "publish\Pic-O-$Runtime"
$ZipPath = "$Output.zip"

if (Test-Path $Output) {
    Remove-Item -LiteralPath $Output -Recurse -Force
}

if (Test-Path $ZipPath) {
    Remove-Item -LiteralPath $ZipPath -Force
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

dotnet publish $Project `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained $selfContainedValue `
    --output $Output

Write-Host "Published to: $Output"

if ($Zip) {
    Compress-Archive -Path (Join-Path $Output "*") -DestinationPath $ZipPath -CompressionLevel Optimal
    Write-Host "Packaged to: $ZipPath"
}
