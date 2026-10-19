# Overenie Hunting online PvP — 30. 9. 2026

## Úspešne overené

- C# skripty sa skompilovali proti nainštalovaným Unity 6000.6.2f1 a Photon PUN knižniciam.
- Prešlo 21 pomenovaných kontrol deterministických bojových pravidiel.
- Prešlo 20 pomenovaných kontrol PUN protokolu, sekvencií, snapshotov, rate limitu a pohybu.
- Celý Unity projekt sa importoval z čistej cache a Windows x64 build skončil výsledkom **Success**.
- Build Settings obsahuje ako prvú scénu `menu` a potom `hunting`.
- Dva samostatné procesy aktuálneho Windows buildu sa pripojili cez Photon Cloud do rovnakej dočasnej miestnosti.
- Host načítal `hunting` ako Player 1 a pripravil lokálne ovládaný `Character1`.
- Druhý klient načítal tú istú scénu ako Player 2 a pripravil lokálne ovládaný `Character2`.
- Obaja klienti vytvorili oba charaktery a úspešne si vymenili autoritatívny stav.

Výsledok posledného automatického behu:

```text
PUN-SMOKE host PASS pair synced local=Character1
PUN-SMOKE guest PASS pair synced local=Character2
PASS: two executable clients joined the same Photon room, loaded hunting,
created Character1/Character2 and exchanged authoritative state.
```

## Čo test pokrýva

Automatický test pokrýva pripojenie, vytvorenie a nájdenie miestnosti, priradenie Photon ActorNumber, synchronizované načítanie scény, vytvorenie dvojice postáv, lokálne vlastníctvo a obojsmerný prenos stavu. Pri zlyhaní alebo odpojení skončí nenulovým exit code a uloží samostatný log hosta a klienta.

Samostatné testy pravidiel pokrývajú takmer súčasný útok a dodge, presnú zhodu kontaktu, neskorý dodge, zásah iba raz, duplicate/replay sekvencie, cooldown, presnú hranicu dosahu, simultánne útoky, snapshot validáciu a obmedzenie pohybu.

## Hranice overenia

- Dva procesy komunikovali cez reálny Photon Cloud, ale bežali na jednom fyzickom počítači. Stále treba manuálny test na dvoch zariadeniach a rozdielnych pripojeniach.
- Automatický test bežal bez grafiky. Overil herný a sieťový stav, nie vzhľad lobby, kameru, animácie ani pocit z pohybu.
- Nebol vykonaný riadený test packet loss, jitteru, dlhého hrania ani násilného prerušenia internetu.
- Master Client je hráčsky host, nie dôveryhodný dedikovaný server. Chráni pred neplatnými vstupmi bežného klienta, nie pred upraveným hostiteľským buildom.
- Pohybová kontrola obmedzuje teleport podľa času a maximálnej rýchlosti. Nejde o kompletnú serverovú fyziku ani rollback/replay celej pôvodnej character simulácie.

## Opakovanie

```powershell
pwsh -File Tests/CombatSimulation.Tests.ps1
pwsh -File Tests/PunCombat.Tests.ps1
pwsh -File Tests/CompilePvp.ps1
pwsh -File Tests/RunPunSmoke.ps1
```

Windows build vytvoríš cez **Tools → PvP → Build Windows Online Demo**. Dvojklientový test potrebuje internet a platné Photon AppIdRealtime. Logy sú v `Tests/PunUnityBuild.log`, `Tests/PunSmoke-host.log` a `Tests/PunSmoke-guest.log`.
