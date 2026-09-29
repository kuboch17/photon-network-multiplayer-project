# Lokálne offline Combat Lab: doplnkový návod

**Aktuálny online režim:** pozri [PUN_NETWORK_GUIDE_SK.md](PUN_NETWORK_GUIDE_SK.md). Lobby a súboj cez PUN sú už implementované. Nasledujúci návod opisuje zachovaný lokálny režim, ktorý sa spustí otvorením hunting bez Photon miestnosti. Aktuálne výsledky testovania sú v [PUN_VERIFICATION.md](PUN_VERIFICATION.md).

## Spustenie priamo v tvojej hre

1. Otvor **Assets/Scenes/hunting.unity** a stlač **Play**.
2. V hornom paneli klikni **Start duel in hunting**.
3. Vyber **Attacker client** alebo **Defender client**, potom **Play** v paneli. Tlačidlo **Step 1 frame** posunie čas o jednu snímku pri 60 fps.
4. Ukáž **Near-simultaneous (+10ms)**, **Exact tie (+100ms)** a **Late dodge (+110ms)**. Výber scenára ho resetuje a pozastaví; znova klikni Play.
5. **Out of range** ukáže minutie, keď je súper príliš ďaleko.
6. **Manual duel → Play**: J = prvý hráč dash, K = prvý hráč dodge, U = druhý hráč dash, I = druhý hráč dodge. Recovery aj cooldown platia pre obe akcie. Ak sa postavy po opakovaných dashoch minú, resetuj kolo.
7. Po resetovaní automatického scenára stlač **Duplicate / cancel spam**, potom Play. Autorita odmietne duplicitný útok a úhyb počas blokovanej akcie. Po skončení cooldownu môže nový príkaz legitímne prijať.
8. **Return to hunting** obnoví pôvodnú postavu, kameru a bežné ovládanie.

Objekt **Hunting PvP Demo** je uložený priamo v hunting.unity a má referenciu na existujúci Character (1). Súper vznikne počas hry z rovnakého modelu. Počas duelu nové pravidlá riadia pohyb a HP skutočne zobrazovaných postáv. Animácie sú prezentačné: nevyhodnocujú poškodenie. Pôvodná fyzikálna bábka je dočasne nahradená zobrazením jej animovaného modelu; pri odchode sa pôvodné komponenty a renderery obnovia.

Je to malý duel po jednej osi v existujúcej scéne, bez voľného chodenia, kolízií so stenami a terénom. Dash ide 1 meter a kontrolovaný dosah je 1,5 metra. Dodge je úhybný pohyb modelu na mieste; autoritatívna pozícia sa pri dodge nemení. Nie je to kompletná náhrada všetkých bojových systémov hry. Staré samostatné Combat Lab menu zostalo dostupné, ale na prezentáciu ho už nepotrebuješ.

## Čo presne vysvetliť

**Problém:** pôvodný bakulisHit.OnTriggerEnter priamo volá hitReceive.hitt. Táto cesta nemá spoločné rozhodnutie o úhybe ani identitu útoku. Je to zistený nedostatok lokálneho návrhu, nie tvrdenie, že sme reprodukovali chybu na produkčnom serveri.

**Riešenie v hunting dueli:** klient odošle zámer; CombatSimulation ako jediná autorita mení HP. HuntingCombatDemo simuluje 60 ms cestu vstupu a 60 ms návrat snapshotu. Lokálna animácia a dash sa predikujú ihneď. HP čaká na potvrdenie. Pri potvrdení sa pozícia opraví podľa snapshotu; ide o jednoduchú okamžitú korekciu, nie plný predikčný replay. Lokálna predikcia sa netriggeruje druhýkrát pri potvrdení rovnakej sekvencie.

**Pravidlá:** celočíselný 10 ms tick, 20 ms vstupný buffer, kontakt 100 ms po začatí dashu, dodge ochrana [začiatok, začiatok +120 ms), recovery 300 ms, cooldown 600 ms. Vstupy rovnakého ticku sa vykonajú pred zásahmi: presnú zhodu vyhrá dodge. Pre vstupy jedného hráča rozhoduje sekvencia. Pri konfliktných duplicitách s rovnakou sekvenciou je poradie podľa druhu akcie stabilné a ďalšia kópia sa odmietne. Simultánne damage sa aplikuje naraz.

Rovnaké vstupy s rovnakými časmi prijatia majú rovnaký výsledok. Iný čas prijatia môže výsledok zmeniť. Buffer nie je rewind ani automatické vyrovnanie pingov.

## Presná časová os pri 120 ms RTT

Predpoklad: 60 ms každým smerom, bez jitteru a straty paketov. Snímky sú prvé zobraziteľné snímky pri 60 fps, číslované od 0. GUI ukazuje aj autoritatívny diagnostický log; ten nie je klientskym potvrdením zásahu.

| Čas / snímka | Útočník | Autorita | Obranca |
|---|---|---|---|
| 0 ms / 0 | Predikuje dash a odošle vstup | Čaká | Stojí |
| 60 ms / 4 | Dash pokračuje | Prijme útok | Stojí |
| 80 ms / 5 | Stále vlastná predikcia | Začne dash, kontakt naplánuje na 180 ms | Ešte nemá vzdialený snapshot |
| 100 ms / 6 | Dosiahne predikovaný cieľ, bez potvrdeného hitu | Dash pokračuje | Vstup dodge: okamžitá lokálna animácia |
| 140 ms / 9 | Príde potvrdenie prijatej akcie a korekcia pozície | Dash pokračuje | Príde prvý snapshot útoku |
| 160 ms / 10 | Čaká na výsledok | Prijme dodge, naplánuje ho na 180 ms | Predikuje dodge |
| 180 ms / 11 | Ešte bez výsledku | Najprv aktivuje dodge, potom kontroluje dosah a kontakt: DODGED | Ešte bez výsledku |
| 240 ms / 15 | Dostane DODGED | HP obrancu 100 | Dostane DODGED, HP 100 |

Pri dodge v **10 ms**: prijatie 70, začiatok 90, ochrana do 210; kontakt 180 je vykrytý.
Pri dodge v **110 ms**: prijatie 170, začiatok 190; kontakt 180 už udelil 25 damage. V 240 ms obaja uvidia potvrdenie. Neskorý úhyb nevie spätne zrušiť zásah.

## Kód, ktorý ukázať

1. **Assets/PvpLab/CombatSimulation.cs — ExecutionTime:** príjem + buffer + pevný tick.
2. **CombatSimulation.AdvanceTo:** najprv vstupy a kontrola sekvencie/cooldownu, potom pohyb a kontakty. Kľúčová je táto postupnosť.
3. **Kontrola avoided / inRange a aplikácia damage:** krátka a čitateľná odpoveď na „kto má posledné slovo?“.
4. **Assets/PvpLab/HuntingCombatDemo.cs — SendInput, VisibleSnapshot, RenderActors:** okamžitá lokálna predikcia oddelená od oneskoreného autoritatívneho výsledku.
5. **Assets/Scripts/hitReceive.cs — hitt:** starý lokálny kontakt nesmie počas duelu obísť nové pravidlá.

### 1. Pevné plánovanie vstupu (skutočný kód)

```csharp
public static int ExecutionTime(int arrival)
{
    return ((arrival + BufferMs + TickMs - 1) / TickMs) * TickMs;
}
```

### 2. Ochrana pred replay a cancel spamom (skutočný kód)

```csharp
if (command.Sequence <= fighter.LastSequence) { Rejected++; continue; }
fighter.LastSequence = command.Sequence;
if (fighter.Hp <= 0 || TimeMs < fighter.BusyUntil || TimeMs < fighter.ReadyAt)
{ Rejected++; Events.Add(TimeMs + " ms: rejected busy/cooldown"); continue; }
```

### 3. Autoritatívne rozhodnutie (skutočný kód)

Tento blok sa vykoná až po spracovaní všetkých vstupov príslušného ticku:

```csharp
bool avoided = TimeMs >= victim.DodgeAt && TimeMs < victim.DodgeAt + InvulnerableMs;
int forwardDistance = (victim.X - Fighters[attacker].X) * Fighters[attacker].Facing;
bool inRange = !spatial || (forwardDistance >= 0 && forwardDistance <= HitRange);
if (inRange && !avoided) damage[defender] += 25;
```

V hunting je spatial zapnuté. Prepínač existuje len kvôli staršiemu časovému Combat Labu.

## Exploit a voľba kompromisu

Najhorší príklad: hráč spätným timestampom predstiera, že uhýbal pred zásahom, alebo ruší útoky do opakovanej nezraniteľnosti bez recovery. Tu príkaz neobsahuje klientom vybraný čas akcie; čas prijatia určuje simulovaný transport. Autorita kontroluje sekvenciu, cooldown a recovery. Cancel počas recovery nie je povolený. Vizuálna predikcia zachová okamžitú odozvu, damage sa nepredikuje.

Pre melee založené na úhyboch je spravidla horšie „dostal som damage po tom, čo som videl úhyb“, lebo hráč stráca dôveru vo vlastnú obranu. Preto pri presnej zhode dávame prednosť dodge. Úplne tento problém neodstraňujeme: neskorý scenár ho zámerne ukazuje. Rozširovanie ochrany alebo spätné rušenie damage by poškodilo útočníka a mohlo otvoriť exploit.

## Hranice, ktoré nezatajiť

- Obaja hráči aj autorita bežia v jednom procese. Žiadny skutočný vzdialený server, Photon prenos ani produkčný multiplayer test.
- Parameter authenticatedActor predstavuje identitu, ktorú musí v reálnej verzii server odvodiť zo spojenia. Lokálny program nie je anticheat hranica.
- Žiadny rollback/rewind, jitter, packet loss, interpolácia alebo kompletná korekcia predikcie. Predikcia je základná a pri potvrdení môže preskočiť pozícia.
- Jednoduchá jednorozmerná geometria, nie fyzikálny melee hitbox alebo kolízia so svetom.
- Pred sieťovým nasadením treba bezpečný transport, limity správ/fronty, validáciu sveta, odpojenie a testy na dvoch zariadeniach. PUN Master Client sa nerovná dôveryhodnému dedikovanému serveru.
- Ukážku si pred odoslaním odpovede osobne vyskúšaj. Neuvádzaj ju ako predchádzajúcu produkčnú skúsenosť.

## Overenie

21 kontrol pravidiel prešlo: timing, replay, cooldown, dosah a jeho hranica, simultánne damage, nezávislé snapshoty a renderovacie snímky. Zmenené C# skripty boli skompilované proti lokálnym Unity 6000.6.2f1 knižniciam. Skontrolované sú aj jedinečné ID nových objektov a referencia hráča v hunting scéne.

Pôvodný pokus o spustenie editora zlyhal na licencii. Pri implementácii online režimu sa podarilo Unity spustiť s lokálnou licenciou a zostaviť Windows build. Aktuálny rozsah runtime overenia uvádza PUN_VERIFICATION.md.

Anglická odpoveď je v **PVP_INTERVIEW_REPLY_EN.md**.
