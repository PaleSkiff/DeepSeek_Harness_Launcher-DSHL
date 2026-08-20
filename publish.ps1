param(
    [string]$Configuration = "Release",
    [string]$Output = "publish"
)

$ErrorActionPreference = "Stop"

# 优先使用用户目录的 .NET SDK（本机安装位置），否则回退到 PATH 中的 dotnet。
$userDotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (Test-Path $userDotnet) {
    $dotnet = $userDotnet
} else {
    $dotnet = "dotnet"
}

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "src\DeepSeekHarnessLauncher\DeepSeekHarnessLauncher.csproj"
$outputDir = Join-Path $root $Output

Write-Host "发布 DeepSeek Harness Launcher ..."
Write-Host "  dotnet: $dotnet"
Write-Host "  config: $Configuration"
Write-Host "  output: $outputDir"

& $dotnet publish $project -c $Configuration -r win-x64 --self-contained true -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "发布失败，退出码 $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host "发布完成：$outputDir" -ForegroundColor Green
