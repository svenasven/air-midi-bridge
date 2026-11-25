$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Нужен .NET Framework 4.x для Windows.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$references = @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Xml.dll')
& $compiler /nologo /target:winexe /platform:anycpu /codepage:65001 /optimize+ "/out:$PSScriptRoot\MR18-MIDI.exe" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки приложения.' }
& $compiler /nologo /target:exe /platform:anycpu /codepage:65001 /optimize+ "/out:$PSScriptRoot\MR18-MIDI-Tests.exe" @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Ошибка сборки проверок.' }
& "$PSScriptRoot\MR18-MIDI-Tests.exe" --self-test
if ($LASTEXITCODE -ne 0) { throw 'Проверки завершились с ошибкой.' }
