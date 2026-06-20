param(
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'
$toolDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = Split-Path -Parent $toolDir

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $rootDir 'ai-session-manager-portable.exe'
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDir = Split-Path -Parent $outputFullPath
$sourcePath = Join-Path $rootDir 'src\AiSessionManagerWpf\Program.cs'
$iconPath = Join-Path $rootDir 'assets\ai-session-manager.ico'

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Cannot find WPF source: $sourcePath"
}

if (-not (Test-Path -LiteralPath $outputDir -PathType Container)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$frameworkRoots = @(
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319')
)

$csc = $null
foreach ($frameworkRoot in $frameworkRoots) {
    $candidate = Join-Path $frameworkRoot 'csc.exe'
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $csc = $candidate
        break
    }
}

if (-not $csc) {
    throw 'Cannot find .NET Framework csc.exe. Install .NET Framework Developer Pack or a .NET SDK with WPF support.'
}

$referenceRoots = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Reference Assemblies\Microsoft\Framework\.NETFramework\v4.0'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\WPF'),
    (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\WPF')
)

$referenceRoot = $null
foreach ($candidateRoot in $referenceRoots) {
    if ((Test-Path -LiteralPath (Join-Path $candidateRoot 'PresentationFramework.dll') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $candidateRoot 'WindowsBase.dll') -PathType Leaf)) {
        $referenceRoot = $candidateRoot
        break
    }
}

if (-not $referenceRoot) {
    throw 'Cannot find .NET Framework WPF reference assemblies.'
}

$references = @(
    'WindowsBase.dll',
    'PresentationCore.dll',
    'PresentationFramework.dll',
    'System.Xaml.dll'
)

$args = New-Object System.Collections.Generic.List[string]
$args.Add('/nologo')
$args.Add('/target:winexe')
$args.Add('/platform:anycpu')
$args.Add('/optimize+')
$args.Add('/codepage:65001')
$args.Add("/out:$outputFullPath")
if (Test-Path -LiteralPath $iconPath -PathType Leaf) {
    $args.Add("/win32icon:$iconPath")
}
foreach ($reference in $references) {
    $referencePath = Join-Path $referenceRoot $reference
    if (-not (Test-Path -LiteralPath $referencePath -PathType Leaf)) {
        $referencePath = $reference
    }
    $args.Add("/reference:$referencePath")
}
$args.Add($sourcePath)

& $csc $args.ToArray()
if ($LASTEXITCODE -ne 0) {
    throw "WPF build failed with exit code $LASTEXITCODE"
}

Write-Host "Built $outputFullPath"
