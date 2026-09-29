$ErrorActionPreference = 'Stop'
Add-Type -Path "$PSScriptRoot/../Assets/PvpLab/CombatSimulation.cs"
function Assert-Equal($actual, $expected, $name) {
    if ($actual -ne $expected) { throw "$name expected $expected, got $actual" }
    Write-Output "PASS: $name"
}
function New-Duel([int]$dodgeInput, [bool]$reverse = $false) {
    $s = [PvpLab.CombatSimulation]::new()
    if ($reverse) { $s.Enqueue(1, 1, [PvpLab.CombatSimulation+ActionKind]::Dodge, ($dodgeInput + 60)) }
    $s.Enqueue(0, 1, [PvpLab.CombatSimulation+ActionKind]::DashAttack, 60)
    if (!$reverse) { $s.Enqueue(1, 1, [PvpLab.CombatSimulation+ActionKind]::Dodge, ($dodgeInput + 60)) }
    return $s
}
$s = New-Duel 10
$s.AdvanceTo(180)
Assert-Equal $s.Fighters[1].Hp 100 'Near-simultaneous dodge'
$s = New-Duel 100
$s.AdvanceTo(170)
Assert-Equal $s.ResolvedAt -1 'No premature result'
$s.AdvanceTo(180)
Assert-Equal $s.Fighters[1].Hp 100 'Dodge wins exact impact tick'
$r = New-Duel 100 $true
$r.AdvanceTo(180)
Assert-Equal ($r.Events -join ',') ($s.Events -join ',') 'Queue insertion order does not change result'
$s = New-Duel 110
$s.AdvanceTo(190)
Assert-Equal $s.Fighters[1].Hp 75 'Late dodge cannot erase damage'
$s.AdvanceTo(1000)
Assert-Equal $s.Fighters[1].Hp 75 'Attack damages once'
$s = New-Duel 10
$s.Enqueue(0, 1, [PvpLab.CombatSimulation+ActionKind]::DashAttack, 70)
$s.Enqueue(0, 2, [PvpLab.CombatSimulation+ActionKind]::Dodge, 80)
$s.AdvanceTo(200)
Assert-Equal $s.Rejected 2 'Duplicate and recovery cancel rejected'
$s = New-Duel 10
$s.Enqueue(0, 2, [PvpLab.CombatSimulation+ActionKind]::DashAttack, 400)
$s.AdvanceTo(500)
Assert-Equal $s.Rejected 1 'Cooldown survives end of recovery'
$s = [PvpLab.CombatSimulation]::new()
$s.Enqueue(1, 1, [PvpLab.CombatSimulation+ActionKind]::Dodge, 0)
$s.Enqueue(0, 1, [PvpLab.CombatSimulation+ActionKind]::DashAttack, 20)
$s.AdvanceTo(140)
Assert-Equal $s.Fighters[1].Hp 75 'Invulnerability end is exclusive'
$s = New-Duel 110
$r = New-Duel 110
0..25 | ForEach-Object { $r.AdvanceTo([int]($_ * 16.6667)) }
$s.AdvanceTo(416)
Assert-Equal ($s.Events -join ',') ($r.Events -join ',') 'Rendering frame rate does not change rules'
$s.Enqueue(7, 1, [PvpLab.CombatSimulation+ActionKind]::Dodge, 500)
$s.Enqueue(0, 2, [PvpLab.CombatSimulation+ActionKind]::Dodge, 100)
Assert-Equal $s.Rejected 2 'Invalid actor and past receipt rejected'
$s = [PvpLab.CombatSimulation]::new($true, 2200)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.Enqueue(1,1,[PvpLab.CombatSimulation+ActionKind]::Dodge,160)
$s.AdvanceTo(180)
Assert-Equal $s.Fighters[0].X 1000 'Authority moves dash one metre'
Assert-Equal $s.Fighters[1].Hp 100 'Spatial duel exact tie dodges'
$s = [PvpLab.CombatSimulation]::new($true, 5000)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.AdvanceTo(180)
Assert-Equal $s.Result 'MISS - out of range' 'Far target takes no damage'
Assert-Equal $s.Fighters[1].Hp 100 'Miss preserves health'
$s = [PvpLab.CombatSimulation]::new($true, 2500)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.AdvanceTo(180)
Assert-Equal $s.Fighters[1].Hp 75 'Exact 1.5 metre reach hits'
$s = [PvpLab.CombatSimulation]::new($true, 2501)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.AdvanceTo(180)
Assert-Equal $s.Fighters[1].Hp 100 'One millimetre outside reach misses'
$s = [PvpLab.CombatSimulation]::new($true, 2200)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.Enqueue(1,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.AdvanceTo(180)
Assert-Equal $s.Fighters[0].Hp 75 'Simultaneous attacks damage first player'
Assert-Equal $s.Fighters[1].Hp 75 'Simultaneous attacks damage second player'
$copy = $s.Fighters[0].Copy()
$copy.Hp = 1
Assert-Equal $s.Fighters[0].Hp 75 'Client snapshot cannot mutate authority health'
$s = [PvpLab.CombatSimulation]::new($true, 2200)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.Enqueue(1,1,[PvpLab.CombatSimulation+ActionKind]::Dodge,170)
$s.AdvanceTo(190)
Assert-Equal $s.Fighters[1].Hp 75 'Spatial late dodge cannot undo hit'
