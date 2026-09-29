# Overenie online PvP — 29. 9. 2026

## Úspešne overené

- **41 pomenovaných kontrol**: 21 kontrol bojových pravidiel a 20 kontrol sieťového protokolu, sekvencií, limitov, snapshotov a pohybu.
- Kompilácia celého Unity projektu a **Windows x64 development build** cez Unity 6000.6.2f1. Posledný proces skončil s exit code 0.
- Kontrola pri zostavení overí, že hunting obsahuje HuntingCombatDemo a platný odkaz na hráča s aktívnym Animatorom.
- **Dva samostatné Windows executable klienty** sa pripojili cez internet do rovnakej dočasnej Photon miestnosti. Host bol actor 1, druhý klient actor 2.
- Lobby, pripojenie, vytvorenie/pripojenie do miestnosti, synchronizované načítanie hunting a potvrdenie pripravenosti oboch klientov prebehli v automatickom teste.
- Oba klienty dokončili tri kolá a porovnanie ich autoritatívnych výsledkov prešlo.

Posledný beh zaznamenal:

| Skúška | Čas kontaktu od začiatku kola | HP P1/P2 na hostovi | HP P1/P2 na klientovi | Výsledok |
|---|---:|---|---|---|
| Mimo dosahu | 440 ms | 100 / 100 | 100 / 100 | MISS |
| Replay a recovery cancel | 480 ms | 100 / 75 | 100 / 75 | HIT; 2 odmietnuté vstupy |
| Úhyb odoslaný +10 ms | 440 ms | 100 / 100 | 100 / 100 | DODGED |

Časy sú namerané v konkrétnom behu. Prvý útok test odošle približne v 200 ms; tabuľka nepredstiera fixnú 120 ms latenciu. Test bol úspešný aj v predchádzajúcich behoch s inými časmi prijatia.

## Čo test odhalil a opravilo sa

Prvý end-to-end beh odhalil chybný duplikovaný odkaz na transform hráča v hunting. Scéna teraz používa svoj pôvodný existujúci transform `372984514`. Nová kontrola pri zostavení by neplatný odkaz odmietla. Po oprave prešli oba klienty všetkými tromi kolami.

Pôvodný problém s licenciou editora sa podarilo vyriešiť spustením Unity s prístupom k lokálnej licencii. Už nejde iba o samostatnú C# kompiláciu: existuje vytvorený a spustený Windows build.

## Hranice overenia

- Oba procesy bežali na **jednom fyzickom počítači**, ale komunikovali cez reálny Photon Cloud. Dve fyzické zariadenia s rôznymi pripojeniami zatiaľ testované neboli.
- Automatické klientske scenáre overili runtime a výsledky boja. Manuálne ovládanie klávesnicou a kompletná vizuálna kontrola UI/animácií zostávajú na playtest v bežnom okne hry.
- Pokus o automatický záber v skrytých oknách poskytol prázdny obrázok, takže obrázky nepoužívame ako dôkaz správneho vykreslenia.
- Nevykonal sa riadený test packet loss/jitter, záťažový test, dlhé hranie ani test reálneho násilného prerušenia internetového spojenia. Príslušné callbacky ukončenia zápasu sú implementované, ale ich kompletná matica zatiaľ nie je overená.
- Jednorozmerný duel nepoužíva kolízie so svetom. Master Client je dôveryhodný v rámci prototypu a nie je chránený pred vlastnou úpravou kódu hostiteľa.

## Opakovanie testov

Spúšťaj každý test pravidiel v novom PowerShell procese (kvôli Add-Type):

```powershell
pwsh -File Tests/CombatSimulation.Tests.ps1
pwsh -File Tests/PunCombat.Tests.ps1
pwsh -File Tests/CompilePvp.ps1
```

Windows build vytvoríš cez **Tools → PvP → Build Windows Online Demo**. Potom:

```powershell
pwsh -File Tests/RunPunSmoke.ps1
```

Tento test použije App ID zabudované v hre, vytvorí náhodnú dočasnú miestnosť, spustí dva procesy, overí tri kolá a procesy ukončí. Vyžaduje internet; spotrebuje dva dočasné Photon sloty. Neotvára cudzie miestnosti a neposiela používateľské správy. Režim bez grafiky nenahrádza manuálny playtest.

Logy: **Tests/PunUnityBuild.log**, **Tests/PunSmoke-host.log**, **Tests/PunSmoke-guest.log**. Test porovnáva rovnaké round ID, čas rozhodnutia, HP, počet odmietnutí a výsledok na oboch stranách.
