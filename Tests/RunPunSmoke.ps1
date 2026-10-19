param([switch]$Graphics)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot/..").Path
$executable = Join-Path $projectRoot 'Builds/HuntingPvP/HuntingPvP.exe'
if (!(Test-Path -LiteralPath $executable)) { throw 'Build the Windows online demo first.' }
$room = 'smoke-' + [guid]::NewGuid().ToString('N').Substring(0,12)
$started = @()
try {
    foreach ($role in @('host','guest')) {
        $log = Join-Path $PSScriptRoot "PunSmoke-$role.log"
        $arguments = @('-duel-role',$role,'-duel-room',$room,'-logFile',('"' + $log + '"'))
        if ($Graphics) {
            $capture = Join-Path $PSScriptRoot "PunSmoke-$role.png"
            $arguments += @('-screen-fullscreen','0','-screen-width','1280','-screen-height','800','-duel-screenshot',('"' + $capture + '"'))
        } else { $arguments += @('-batchmode','-nographics') }
        $started += Start-Process -FilePath $executable -ArgumentList $arguments -WindowStyle Hidden -PassThru
    }
    $deadline = [DateTime]::UtcNow.AddSeconds(110)
    while (($started | Where-Object { !$_.HasExited }) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 200 }
    foreach ($process in $started) {
        if (!$process.HasExited) { throw 'Online smoke test exceeded its deadline.' }
        if ($process.ExitCode -ne 0) { throw "Online smoke process failed: exit $($process.ExitCode). Inspect PunSmoke logs." }
    }
    foreach ($role in @('host','guest')) {
        $lines = Get-Content (Join-Path $PSScriptRoot "PunSmoke-$role.log")
        if (!($lines -match "PUN-SMOKE $role PASS")) { throw "$role did not report success." }
        @($lines | Where-Object { $_ -match "^PUN-SMOKE $role PASS" }) | Write-Output
    }
    'PASS: two executable clients joined the same Photon room, loaded hunting, created Character1/Character2 and exchanged authoritative state.'
}
finally {
    # These are only the exact child processes created by this test invocation.
    foreach ($process in $started) { if (!$process.HasExited) { $process.Kill() } }
}
