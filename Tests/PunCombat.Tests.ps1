$ErrorActionPreference = 'Stop'
Add-Type -Path @("$PSScriptRoot/../Assets/PvpLab/CombatSimulation.cs", "$PSScriptRoot/../Assets/PvpLab/PunCombatProtocol.cs")
function Check($actual, $expected, [string]$name) {
    if ($actual -ne $expected) { throw "$name : expected $expected, got $actual" }
    "PASS: $name"
}
$round = 'test-round'
$s = [PvpLab.CombatSimulation]::new($true,2200)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.Enqueue(1,1,[PvpLab.CombatSimulation+ActionKind]::Dodge,160)
$s.AdvanceTo(180)
$wire = [PvpLab.PunCombatProtocol]::Encode($round,$s,0)
$snapshot = $null
Check ([PvpLab.PunCombatProtocol]::TrySnapshot($wire,$round,-1,[ref]$snapshot)) $true 'Snapshot round-trip'
Check $snapshot.Fighters[1].Hp 100 'Remote sees authoritative dodge result'
Check ([PvpLab.PunCombatProtocol]::TrySnapshot($wire,$round,180,[ref]$snapshot)) $false 'Duplicate snapshot ignored'
Check ([PvpLab.PunCombatProtocol]::TrySnapshot($wire,'next-round',-1,[ref]$snapshot)) $false 'Old-round snapshot ignored'
$wire[5] = [int[]]@(500,1,1,1,1,1,1,1,1,1,1,1,1)
Check ([PvpLab.PunCombatProtocol]::TrySnapshot($wire,$round,-1,[ref]$snapshot)) $false 'Malformed health rejected'
$seq=0; $kind=[PvpLab.CombatSimulation+ActionKind]::Stop
Check ([PvpLab.PunCombatProtocol]::TryInput([object[]]@($round,1,[byte]1),$round,[ref]$seq,[ref]$kind)) $true 'Valid intent accepted'
Check ([PvpLab.PunCombatProtocol]::TryInput([object[]]@($round,1,[byte]1,999),$round,[ref]$seq,[ref]$kind)) $false 'Extra forged payload rejected'
Check ([PvpLab.PunCombatProtocol]::TryInput([object[]]@('old',1,[byte]1),$round,[ref]$seq,[ref]$kind)) $false 'Previous-round command ignored'
$gate=[PvpLab.PunCombatProtocol+InputGate]::new()
Check ($gate.Accept(0,1,0)) $true 'First sequence accepted'
Check ($gate.Accept(0,1,1)) $false 'Replayed sequence rejected'
Check ($gate.Accept(-1,2,2)) $false 'Unassigned actor rejected'
Check ($gate.Accept(0,999999,2)) $false 'Sequence jump rejected'
2..30 | ForEach-Object { if (!$gate.Accept(0,$_,10)) {throw 'Unexpected rate reject'} }
Check ($gate.Accept(0,31,20)) $false 'Input rate bounded'
Check ($gate.Accept(0,31,1010)) $true 'Rate window recovers'
Check ([PvpLab.PunCombatProtocol]::Elapsed([int]::MinValue,[int]::MaxValue)) 1 'Server timestamp wraparound'
$s=[PvpLab.CombatSimulation]::new($true,2200)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::MoveLeft,0)
$s.AdvanceTo(500)
Check $s.Fighters[0].X -750 'Movement lease expires after lost release'
$s=[PvpLab.CombatSimulation]::new($true,2200)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,60)
$s.Enqueue(0,2,[PvpLab.CombatSimulation+ActionKind]::MoveLeft,70)
$s.AdvanceTo(300)
Check $s.Fighters[0].X 1000 'Movement cannot cancel attack recovery'
$s=[PvpLab.CombatSimulation]::new($true,2200)
$s.Fighters[0].X=-5990
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::MoveLeft,0)
$s.AdvanceTo(100)
Check $s.Fighters[0].X -6000 'Arena bounds enforced'
Check $s.Fighters[1].Hp 100 'Movement command does not attack'
$s=[PvpLab.CombatSimulation]::new($true,2200)
$s.Enqueue(0,1,[PvpLab.CombatSimulation+ActionKind]::DashAttack,0)
$s.AdvanceTo(50)
$s.Fighters[0].Hp=0
$s.AdvanceTo(150)
Check $s.Fighters[1].Hp 100 'Dead attacker cannot resolve delayed hit'
