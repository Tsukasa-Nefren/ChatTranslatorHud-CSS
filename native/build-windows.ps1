param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $root "runtimes\win-x64\native"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

function Select-ExistingPath([string[]]$Paths, [string]$Description) {
    foreach ($path in $Paths) {
        $resolved = Resolve-Path -LiteralPath $path -ErrorAction SilentlyContinue
        if ($resolved) {
            return $resolved.Path
        }
    }

    throw "$Description was not found. Checked: $($Paths -join ', ')"
}

$cl = Get-ChildItem -Path "${env:ProgramFiles(x86)}\Microsoft Visual Studio" -Recurse -Filter cl.exe |
    Where-Object { $_.FullName -like "*\VC\Tools\MSVC\*\bin\Hostx64\x64\cl.exe" } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $cl) {
    throw "cl.exe was not found. Install Visual Studio Build Tools with the C++ toolchain."
}

$vcTools = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $cl.FullName)))
$include = Join-Path $vcTools "include"
$lib = Join-Path $vcTools "lib\x64"
$sdkRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10"
$sdkIncludeBase = Join-Path $sdkRoot "Include"
$sdkLibBase = Join-Path $sdkRoot "Lib"
$sdkVersion = Get-ChildItem -Path $sdkIncludeBase -Directory |
    Sort-Object Name -Descending |
    Select-Object -First 1

if (-not $sdkVersion) {
    throw "Windows SDK include directory was not found."
}

$sdkInclude = Join-Path $sdkIncludeBase $sdkVersion.Name
$sdkLib = Join-Path $sdkLibBase $sdkVersion.Name

$env:INCLUDE = @(
    $include,
    (Join-Path $sdkInclude "ucrt"),
    (Join-Path $sdkInclude "um"),
    (Join-Path $sdkInclude "shared")
) -join ";"

$protoInclude = Select-ExistingPath @(
    (Join-Path $root "..\..\modsharp-public-master\Engine\src\proto")
) "ModSharp generated protobuf include directory"

$protobufInclude = Select-ExistingPath @(
    (Join-Path $root "..\CounterStrikeSharp\libraries\hl2sdk-cs2\thirdparty\protobuf-3.21.8\src"),
    (Join-Path $root "..\..\_tmp\CounterStrikeSharp\libraries\hl2sdk-cs2\thirdparty\protobuf-3.21.8\src")
) "protobuf include directory"

$protobufLib = Select-ExistingPath @(
    (Join-Path $root "..\CounterStrikeSharp\libraries\hl2sdk-cs2\lib\public\win64\2015"),
    (Join-Path $root "..\..\_tmp\CounterStrikeSharp\libraries\hl2sdk-cs2\lib\public\win64\2015"),
    (Join-Path $root "..\..\modsharp-public-master\Engine\lib")
) "protobuf library directory"

$dynoHookInclude = Select-ExistingPath @(
    (Join-Path $root "..\CounterStrikeSharp\libraries\DynoHook\src"),
    (Join-Path $root "..\..\_tmp\CounterStrikeSharp\libraries\DynoHook\src")
) "DynoHook include directory"

$env:LIB = @(
    $lib,
    (Join-Path $sdkLib "ucrt\x64"),
    (Join-Path $sdkLib "um\x64"),
    $protobufLib
) -join ";"

$clArgs = @(
    "/nologo",
    "/LD",
    "/O2",
    "/EHsc",
    "/std:c++20",
    "/I$protoInclude",
    "/I$protobufInclude",
    "/I$dynoHookInclude",
    (Join-Path $PSScriptRoot "ChatTranslatorHud.Native.cpp"),
    "/Fo:$(Join-Path $outDir "ChatTranslatorHud.Native.obj")",
    "/Fe:$(Join-Path $outDir "ChatTranslatorHud.Native.dll")",
    "/link",
    "libprotobuf.lib"
)

& $cl.FullName @clArgs | Write-Output

Remove-Item -LiteralPath (Join-Path $outDir "ChatTranslatorHud.Native.obj") -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $outDir "ChatTranslatorHud.Native.lib") -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $outDir "ChatTranslatorHud.Native.exp") -Force -ErrorAction SilentlyContinue
