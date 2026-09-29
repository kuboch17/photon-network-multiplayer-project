# Hunting online PvP cez Photon PUN

## Čo sa zmenilo

Prvá scéna buildu je teraz pôvodná `Assets/Scenes/menu.unity`. Jej UI som zjednodušil na jedno pole **Room code** a dve tlačidlá **CREATE ROOM** a **JOIN ROOM**. Po pripojení druhého hráča host automaticky načíta scénu `hunting` obom klientom.

V `hunting` zostáva pôvodný objekt aj jeho existujúce ovládacie skripty. Volá sa **Character1**. Pri štarte zápasu sa z neho vytvorí vizuálne rovnaký **Character2**.

- prvý hráč ovláda `Character1`,
- druhý hráč ovláda `Character2`,
- každý počítač má aktívne input a movement skripty iba na svojej postave,
- cudziu postavu riadia potvrdené sieťové stavy,
- kamera sa automaticky prepne na lokálne vlastnenú postavu.

Postavy sa nevytvárajú cez `PhotonNetwork.Instantiate`. Obe scény poznajú presne tie isté dva objekty a `PhotonNetwork.RaiseEvent` prenáša ich stav. Pri tomto pevnom prototype je to jednoduchšie: netreba Resources prefab ani dynamické Photon View ID. Stále ide o reálny PUN multiplayer, pretože pohybové návrhy, autoritatívne stavy aj bojové vstupy idú cez Photon Cloud.

## Ako to spustiť na dvoch počítačoch

1. Otvor projekt v Unity 6000.6.2f1.
2. Over, že `PhotonServerSettings.asset` obsahuje platné AppIdRealtime.
3. V Unity spusti scénu `Assets/Scenes/menu.unity`, alebo vytvor Windows build cez **Tools → PvP → Build Windows Online Demo**.
4. Na druhý počítač skopíruj celý priečinok `Builds/HuntingPvP`, nie iba EXE.
5. Na prvom počítači zadaj napríklad `test123` a klikni **CREATE ROOM**.
6. Na druhom zadaj rovnaký kód a klikni **JOIN ROOM**.
7. Keď sa pripojí druhý hráč, `hunting` sa spustí automaticky.
8. Každý používa pôvodné ovládanie postavy. Navyše **J** spustí demonštračný dash attack a **K** dodge.

Vľavo hore sa zobrazí vlastnená postava, Photon ping, HP oboch postáv, posledné rozhodnutie hostiteľa a počet odmietnutých vstupov.

## Ako funguje sieť

### 1. Lobby priradí hráčov

`RoomManager.cs` vytvorí súkromnú miestnosť pre dvoch. Host uloží do room properties identifikátor zápasu a Photon ActorNumber pre Player 1 a Player 2. Potom použije `PhotonNetwork.LoadLevel("hunting")`, takže PUN načíta rovnakú scénu obom.

### 2. Pôvodné ovládanie zostáva lokálne

`HuntingCombatDemo.ConfigureCharacter` nastaví na vlastnej postave pôvodný `RPGCharacterInputController` na Player 1 vstupy. Na cudzej postave vypne všetky `MonoBehaviour` herné skripty a nastaví rigidbody na kinematic. Cudzí klient preto nemôže ovládať tvoju postavu a dve lokálne simulácie sa nebijú.

### 3. Pohyb potvrdzuje host

Lokálny klient každých približne 50 ms pošle hostiteľovi navrhovanú pozíciu, rotáciu, rýchlosť a základné animačné hodnoty:

```csharp
PhotonNetwork.RaiseEvent(
    MovementProposalEvent,
    packet,
    new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
    SendOptions.SendUnreliable);
```

Host odvodí identitu zo `photonEvent.Sender`, kontroluje rastúcu sekvenciu a maximálnu vzdialenosť podľa času. Príliš veľký teleport oreže a započíta ako odmietnutý vstup. Potvrdený plný stav odošle druhému klientovi. Ten cudziu postavu plynulo interpoluje.

Toto je jednoduchá host-authoritative validácia pohybu, nie kompletný rollback systém. Master Client je stále počítač jedného hráča; pre produkčný anticheat by autorita mala bežať na dedikovanom serveri.

### 4. Útok a dodge rozhoduje host

Klient posiela iba druh akcie a sekvenciu. Neposiela damage ani výsledok. Host kontroluje vlastníka, poradie, cooldown, HP a dosah.

```csharp
if (slot != SlotForActor(sender) ||
    seq <= lastActionSequence[slot] ||
    now < readyAt[slot] || hp[slot] <= 0)
{
    rejectedInputs++;
    return;
}
```

Dash attack má 100 ms windup, dosah 2,2 m a 25 damage. Dodge má 120 ms ochranné okno. Host čaká ďalších 30 ms pred finálnym rozhodnutím; rovnakých 30 ms je maximálna dodge leniency. Tým sa zlepší pocit pri tesnom úhybe, ale klient nedostane neobmedzenú možnosť spätne meniť výsledok.

```csharp
bool dodged = dodgeFrom[defender] <= contact
    && dodgeUntil[defender] >= contact;
float distance = Vector3.Distance(
    authorityPosition[attacker],
    authorityPosition[defender]);

if (!dodged && distance <= AttackRange)
    hp[defender] = Mathf.Max(0, hp[defender] - 25);
```

## Čo ukázať interviewerovi

1. Spusť dva klienty a ukáž, že jeden ovláda `Character1`, druhý `Character2`.
2. Pohni druhou postavou a ukáž interpoláciu na prvom počítači.
3. Postav ich ďalej než 2,2 m a stlač J: host zobrazí **MISS**.
4. Postav ich blízko a stlač J: po potvrdení sa odpočíta 25 HP.
5. Nech obranca takmer súčasne stlačí K: panel ukáže **DODGE**, ak ochranné okno pokrýva čas kontaktu.
6. Vysvetli, že animácia je okamžitá lokálna odozva, ale HP sa mení až po rozhodnutí hostiteľa.

## 120 ms RTT scenár

Pri zjednodušenom symetrickom RTT 120 ms je cesta vstupu k hostiteľovi približne 60 ms.

- 0 ms: útočník stlačí dash attack a hneď vidí animáciu.
- približne 60 ms: host prijme útok a určí kontakt na +100 ms, teda približne 160 ms.
- obranca stlačí dodge skoro v rovnakom čase a tiež ho lokálne vidí okamžite.
- keď host prijme dodge pred kontaktom, jeho 120 ms okno pokryje kontakt a výsledok je DODGE.
- ak dodge príde tesne neskôr, pevná 30 ms leniency ho môže ešte prijať.
- po tejto hranici zásah platí; neskorý dodge už potvrdené HP nevráti.
- potvrdenie sa vracia cez Photon a klient ho zobrazí v najbližšom renderovanom frame.

Presný frame nemožno sľúbiť iba z RTT. Ovplyvňuje ho jitter, 50 ms interval stavov a renderovací čas. Pri 60 FPS má jeden frame približne 16,7 ms.

## Najhorší exploit a ochrana

Najhorší exploit by bol, keby klient po zistení zásahu poslal spätne datovaný dodge, opakoval starú správu alebo sa teleportoval do dosahu a sám oznámil damage. Implementácia preto:

- identitu berie z Photon sendera,
- vyžaduje rastúce sekvencie,
- neprijíma klientom zadaný damage ani výsledok,
- cooldown a HP drží host,
- vzdialenosť počíta z hostom potvrdených pozícií,
- obmedzuje leniency na pevných 30 ms,
- po odchode hostiteľa zápas bezpečne ukončí.

Tak sa zachová okamžitá animácia a ovládanie, ale rozhodujúci stav zostáva na jednej autorite.

## Dôležité súbory

- `Assets/Scenes/menu.unity` – jednoduché lobby UI.
- `Assets/Photon/RoomManager.cs` – Photon pripojenie, room a spoločné načítanie.
- `Assets/Scenes/hunting.unity` – pôvodná herná scéna s `Character1`.
- `Assets/PvpLab/HuntingCombatDemo.cs` – vytvorenie `Character2`, vlastníctvo, pohyb, animácia a autoritatívny boj.
- `Assets/PvpLab/PunCombatProtocol.cs` – spoločná verzia a room properties.
- `Assets/PvpLab/CombatSimulation.cs` – izolovaná deterministická ukážka a hraničné testy.
- `Assets/PvpLab/Editor/PunBuild.cs` – Windows build s menu a hunting.
- `Tests/CombatSimulation.Tests.ps1`, `Tests/PunCombat.Tests.ps1`, `Tests/CompilePvp.ps1` – automatické kontroly.

## Aktuálne overenie

Kód prešiel kompiláciou proti nainštalovaným Unity a PUN knižniciam. Prešlo 21 kontrol bojových pravidiel a 20 kontrol protokolu. Po tejto zmene ešte treba urobiť bežný vizuálny playtest dvoch okien alebo dvoch počítačov; automatické testy nenahrádzajú kontrolu kamery, animácií a pocitu z pohybu.
