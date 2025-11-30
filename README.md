# Duelists_CLI_Clone_Client_Server
A CLI Client Server Project to play a small version off the card Game "Duelists"

Bestandteil einer Uni Abgabe in der Tool und Backend Entwicklung. 
Kernidee: Umsetzen einer runterskalierten Version des Kartenspiels Duelists. 

## Spielregeln:
- Jeder Spieler besitzt 3 feste Karten und ordnet sie zu Beginn jeder Runde neu.
- Jede Karte hat eine Angriffsrange (Angriffsformel) und eine feste Anzahl an Verteidigungsversuchen.

**Karten:**

Karte A: Angriff 1–5, Verteidigung 1

Karte B: Angriff 1–4 Bonus bei Treffer, Verteidigung 2

Karte C: Angriff 1–2, Verteidigung 3

**Duellablauf:**

- Wer zuerst angreift, wird zu Beginn ausgewürfelt.
- Angreifer wählt eine Zahl in seiner Angriffsrange.
- Verteidiger hat entsprechend seiner Karte X Versuche, die Zahl zu erraten.

-Treffer → kein Schaden

    -Kein Treffer → Schaden = Angriffsnummer (+ Bonus bei Karte 2)

- Danach muss die Verteidigende Karte auch angreifen und die Rollen vertauschen sich. (Jede Karte muss einmal angreifen und einmal verteidigen)

**Runden:**

- Eine Runde besteht aus 3 Duellen (je Karte).
- Danach ordnen beide Spieler ihre Karten neu.

**Sieg:**

- Jeder Spieler startet mit 14 HP.
- Wer zuerst 0 HP erreicht, verliert.

----------------------------------------
## Umsetzen des Servers:
- Client kommuniziert auf dem Server nur mittels Commands die vom Server vorgegeben sind.
- Command wechseln je nach Spiel oder Lobbysituation des Spielers
- Lobbysystem von bis zu 2 Spielern

**Commands die beim joinen möglich sind ohne, dass man sich in einer Lobby befindet:**
```
  NetworkUtils.SafeSend(p, "[SERVER] /create <name> - Lobby erstellen");
  NetworkUtils.SafeSend(p, "[SERVER] /join <name> - Lobby beitreten");
  NetworkUtils.SafeSend(p, "[SERVER] /duels - offene Lobbys anzeigen");
  NetworkUtils.SafeSend(p, "[SERVER] /chat <msg> - Globalen Chat senden");
```

**Lobby commands um sich festzulegen, seine Karten Reihenfolge zu definieren, ready status, etc.**
```
  NetworkUtils.SafeSend(p, "[SERVER] /leave - Lobby verlassen");
  NetworkUtils.SafeSend(p, "[SERVER] /cards - zeigt deine Karten");
  NetworkUtils.SafeSend(p, "[SERVER] /order <A B C> - Reihenfolge festlegen");
  NetworkUtils.SafeSend(p, "[SERVER] /ready - Ready toggeln");
  NetworkUtils.SafeSend(p, "[SERVER] /start - nur Host startet Duell");
  NetworkUtils.SafeSend(p, "[SERVER] /chat <msg> - Lobby-Chat senden");
```

**gameplay commands**
```
  NetworkUtils.SafeSend(p, "[SERVER] /leave - Duell verlassen");
  NetworkUtils.SafeSend(p, "[SERVER] /chat <msg> - Duell-Chat senden");
  NetworkUtils.SafeSend(p, "[SERVER] /stats - Zeigt Stats auf -> HP -> Karten etc.");
  NetworkUtils.SafeSend(p, "[SERVER] /attack <value> - abhängig von angriffsrange");
  NetworkUtils.SafeSend(p, "[SERVER] /defend <value> <value> ... - abhängig von defense value");
```

