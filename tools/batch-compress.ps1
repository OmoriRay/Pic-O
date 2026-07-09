param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [string]$OutputFolder = "",

    [ValidateRange(1, 100)]
    [int]$Quality = 82,

    [int]$MaxWidth = 0,

    [int]$MaxHeight = 0,

    [ValidateSet("jpg", "png", "webp")]
    [string]$Format = "jpg",

    [switch]$Recurse
)

$ErrorActionPreference = "Stop"

$magick = Get-Command magick -ErrorAction SilentlyContinue
if (-not $magick) {
    throw "ImageMagick CLI 'magick' was not found. Install ImageMagick, then run this script again."
}

if (-not (Test-Path -LiteralPath $InputPath)) {
    throw "InputPath does not exist: $InputPath"
}

$supportedExtensions = @(".jpg", ".jpeg", ".jpe", ".jfif", ".png", ".bmp", ".webp", ".tif", ".tiff", ".heic", ".heif", ".avif")
$inputItem = Get-Item -LiteralPath $InputPath
$files = if ($inputItem.PSIsContainer) {
    Get-ChildItem -LiteralPath $inputItem.FullName -File -Recurse:$Recurse |
        Where-Object { $supportedExtensions -contains $_.Extension.ToLowerInvariant() }
} else {
    @($inputItem)
}

if ($files.Count -eq 0) {
    Write-Host "No supported image files found."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($OutputFolder)) {
    $OutputFolder = if ($inputItem.PSIsContainer) {
        Join-Path $inputItem.FullName "compressed"
    } else {
        Join-Path $inputItem.DirectoryName "compressed"
    }
}

New-Item -ItemType Directory -Path $OutputFolder -Force | Out-Null

$resize = ""
if ($MaxWidth -gt 0 -or $MaxHeight -gt 0) {
    $resize = "$MaxWidth" + "x" + "$MaxHeight" + ">"
}

$done = 0
foreach ($file in $files) {
    $relativeName = if ($inputItem.PSIsContainer) {
        [IO.Path]::GetRelativePath($inputItem.FullName, $file.FullName)
    } else {
        $file.Name
    }

    $relativeFolder = Split-Path $relativeName -Parent
    $targetFolder = if ([string]::IsNullOrWhiteSpace($relativeFolder)) {
        $OutputFolder
    } else {
        Join-Path $OutputFolder $relativeFolder
    }

    New-Item -ItemType Directory -Path $targetFolder -Force | Out-Null

    $targetName = [IO.Path]::GetFileNameWithoutExtension($file.Name) + "." + $Format
    $targetPath = Join-Path $targetFolder $targetName
    $args = @($file.FullName, "-auto-orient")

    if (-not [string]::IsNullOrWhiteSpace($resize)) {
        $args += @("-resize", $resize)
    }

    if ($Format -eq "jpg" -or $Format -eq "webp") {
        $args += @("-quality", $Quality)
    }

    $args += $targetPath
    & $magick.Source @args
    $done++
    Write-Host "[$done/$($files.Count)] $($file.Name) -> $targetPath"
}

Write-Host "Done. Output: $OutputFolder"
