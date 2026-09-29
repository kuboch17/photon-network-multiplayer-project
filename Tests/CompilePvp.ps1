param([string]$UnityData = 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Data')
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/..").Path
$arguments = @('-nologo','-target:library','-nostdlib+',('-out:"' + "$PSScriptRoot/PvpCompileCheck.dll" + '"'))
$referenceFolders = @("$UnityData/UnityReferenceAssemblies/unity-4.8-api", "$UnityData/UnityReferenceAssemblies/unity-4.8-api/Facades", "$UnityData/Managed/UnityEngine")
foreach ($folder in $referenceFolders) {
    $arguments += Get-ChildItem $folder -Filter '*.dll' | ForEach-Object { '-r:"' + $_.FullName + '"' }
}
foreach ($relative in @('Library/ScriptAssemblies/PhotonRealtime.dll','Library/ScriptAssemblies/PhotonUnityNetworking.dll','Library/ScriptAssemblies/UnityEngine.UI.dll','Assets/Photon/PhotonLibs/Photon3Unity3D.dll')) {
    $arguments += '-r:"' + (Join-Path $projectRoot $relative) + '"'
}
$arguments += Get-ChildItem "$projectRoot/Assets/PvpLab" -Filter '*.cs' -Recurse | ForEach-Object { '"' + $_.FullName + '"' }
$arguments += '"' + "$projectRoot/Assets/Scripts/hitReceive.cs" + '"'
$arguments += '"' + "$projectRoot/Assets/Photon/RoomManager.cs" + '"'
$arguments | Set-Content -Encoding utf8 "$PSScriptRoot/PvpCompile.rsp"
& "$UnityData/DotNetSdk/dotnet.exe" "$UnityData/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll" "@$PSScriptRoot/PvpCompile.rsp" 2>&1 | Tee-Object "$PSScriptRoot/PvpCompile.log"
if ($LASTEXITCODE -ne 0) { throw 'PvP script compilation failed' }
'PASS: PvP scripts compile against installed Unity and Photon assemblies' | Tee-Object -Append "$PSScriptRoot/PvpCompile.log"
