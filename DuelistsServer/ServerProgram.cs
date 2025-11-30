using DuelistsServer;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;


class Program
{
    private static TcpListener listener;
    private static readonly Dictionary<TcpClient, Player> players = new();
    private static readonly Dictionary<string, Lobby> lobbies = new();
    private static readonly object playersLock = new();
    private static readonly object lobbiesLock = new();

    static void Main()
    {
        Log.PrintBanner();
        listener = new TcpListener(IPAddress.Any, 9000);
        listener.Start();
        Log.Info("Server gestartet auf Port 9000");
        Log.PrintLocalIPs();

        while (true)
        {
            try
            {
                TcpClient client = listener.AcceptTcpClient();
                var endpoint = (IPEndPoint)client.Client.RemoteEndPoint;

                Player p = new Player
                {
                    Client = client,
                    Stream = client.GetStream(),
                    IPAddress = endpoint.Address.ToString(),
                    Port = endpoint.Port
                };

                lock (playersLock) players[client] = p;
                Log.Info($"Neuer Client verbunden: {p.IPAddress}:{p.Port}");

                Thread t = new Thread(() => HandleConnection(p)) { IsBackground = true };
                t.Start();
            }
            catch (Exception ex)
            {
                Log.Error($"Fehler beim Akzeptieren eines Clients: {ex.Message}");
            }
        }
    }

    
    static void HandleConnection(Player p)
    {
        if (p == null) return;

        byte[] buffer = new byte[2048];
        var incoming = new StringBuilder();
        Log.Debug($"Warte auf USERNAME von {p.IPAddress}:{p.Port}...");

        try
        {
            while (string.IsNullOrEmpty(p.Username))
            {
                if (NetworkUtils.IsDisconnected(p)) { Log.Warn($"Verbindung getrennt vor Username: {p.IPAddress}:{p.Port}"); return; }

                if (p.Stream.DataAvailable)
                {
                    int bytes = NetworkUtils.SafeRead(p, buffer);
                    if (bytes <= 0) return;

                    incoming.Append(Encoding.UTF8.GetString(buffer, 0, bytes));
                    ProcessLines(incoming, line =>
                    {
                        var parts = line.Split('|', 2);
                        if (parts.Length == 2 && parts[0] == "USERNAME")
                        {
                            string desired = parts[1].Trim();
                            if (string.IsNullOrEmpty(desired))
                            {
                                NetworkUtils.SafeSend(p, "[ERROR] Username darf nicht leer sein.");
                                return;
                            }

                            bool exists = false;
                            lock (playersLock)
                            {
                                foreach (var pl in players.Values)
                                {
                                    if (pl.Username != null && pl.Username.Equals(desired, StringComparison.OrdinalIgnoreCase))
                                    {
                                        exists = true; break;
                                    }
                                }
                            }

                            if (exists)
                            {
                                Log.Warn($"USERNAME abgelehnt (existiert): {desired} von {p.IPAddress}:{p.Port}");
                                NetworkUtils.SafeSend(p, "[ERROR] Username existiert bereits. Bitte anderen wählen: USERNAME|<name>");
                                return;
                            }

                            p.Username = desired;
                            Log.Info($"Username gesetzt: {p.Username} ({p.IPAddress}:{p.Port})");
                            NetworkUtils.SafeSend(p, $"[SERVER] Willkommen, {p.Username}!");
                            Log.ShowHelp(p);
                        }
                        else
                        {
                            NetworkUtils.SafeSend(p, "[ERROR] Bitte Username senden: USERNAME|<name>");
                        }
                    });
                }
                else Thread.Sleep(15);
            }

            // COMMAND-LOOP
            while (true)
            {
                if (NetworkUtils.IsDisconnected(p)) { Log.Warn($"Client getrennt: {p.Username ?? "Unknown"} ({p.IPAddress}:{p.Port})"); break; }

                if (p.Stream.DataAvailable)
                {
                    int bytes = NetworkUtils.SafeRead(p, buffer);
                    if (bytes <= 0) break;

                    incoming.Append(Encoding.UTF8.GetString(buffer, 0, bytes));
                    ProcessLines(incoming, line =>
                    {
                        if (string.IsNullOrWhiteSpace(line)) return;

                        var parts = line.Split('|', 2);
                        string cmd = parts[0].Trim();
                        string arg = parts.Length > 1 ? parts[1] : "";

                        Log.Info($"COMMAND von {p.Username}: {line}");
                        HandleCommand(p, cmd, arg);
                    });
                }
                else Thread.Sleep(15);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Exception im Client-Thread ({p.Username ?? p.IPAddress}): {ex.Message}");
        }
        finally
        {
            Disconnect(p);
        }
    }

    static void ProcessLines(StringBuilder incoming, Action<string> handleLine)
    {
        while (true)
        {
            string all = incoming.ToString();
            int idx = all.IndexOf('\n');
            if (idx < 0) break;

            string line = all.Substring(0, idx).Trim('\r', '\n', ' ');
            incoming.Remove(0, idx + 1);
            try { handleLine(line); }
            catch (Exception ex) { Log.Error($"Fehler beim Verarbeiten einer Zeile: {ex.Message}"); }
        }
    }



    // Command Management
    static void HandleCommand(Player p, string cmd, string arg)
    {
        if (string.Equals(cmd, "/help", StringComparison.OrdinalIgnoreCase)) { Log.ShowHelp(p); return; }

        switch (p.State)
        {
            case PlayerState.NotInLobby:
                HandleNotInLobbyCommands(p, cmd, arg); break;
            case PlayerState.InLobby:
                HandleLobbyCommands(p, cmd, arg); break;
            case PlayerState.InDuel:
                HandleDuelCommands(p, cmd, arg); break;
        }
    }

    static void HandleNotInLobbyCommands(Player p, string cmd, string arg)
    {
        switch (cmd.ToLowerInvariant())
        {
            case "/create": CreateLobby(p, arg); break;
            case "/join": JoinLobby(p, arg); break;
            case "/duels": ListLobbies(p); break;
            case "/chat": BroadcastGlobalChat(p, arg); break;
            default:
                NetworkUtils.SafeSend(p, "[ERROR] Ungültiger Command. /help");
                break;
        }
    }

    static void HandleLobbyCommands(Player p, string cmd, string arg)
    {
        if (p.CurrentLobby == null) { p.State = PlayerState.NotInLobby; NetworkUtils.SafeSend(p, "[ERROR] Lobby existiert nicht."); return; }
        var lobby = p.CurrentLobby;

        switch (cmd.ToLowerInvariant())
        {
            case "/chat": lobby.Broadcast($"{p.Username}: {arg}"); break;
            case "/leave": LeaveLobby(p); break;
            case "/cards": ShowCards(p); break;
            case "/order": SetOrder(p, arg); break;
            case "/ready": ToggleReady(p); break;
            case "/start": StartGame(p); break;
            default:
                NetworkUtils.SafeSend(p, "[ERROR] Ungültiger Command in Lobby. /help");
                break;
        }
    }

    static void HandleDuelCommands(Player p, string cmd, string arg)
    {
        var lobby = p.CurrentLobby;
        if (lobby == null || lobby.Duel == null) { NetworkUtils.SafeSend(p, "[ERROR] Kein aktives Duell."); return; }

        switch (cmd.ToLowerInvariant())
        {
            case "/chat":
                lobby.Broadcast($"{p.Username}: {arg}");
                break;

            case "/attack":
                Combat.ExecuteAttack(p, lobby, arg);
                break;

            case "/defend":
                Combat.ExecuteDefense(p, lobby, arg);
                break;

            case "/stats":
                Log.ShowStats(p, lobby);
                break;

            case "/leave":
                LeaveLobby(p);
                break;

            default:
                NetworkUtils.SafeSend(p, "[ERROR] Ungültiger Command im Duell. /help");
                break;
        }
    }



    // Lobby Management
    static void CreateLobby(Player p, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) { NetworkUtils.SafeSend(p, "[ERROR] Name fehlt."); return; }

        lock (lobbiesLock)
        {
            if (lobbies.ContainsKey(name)) { NetworkUtils.SafeSend(p, "[ERROR] Lobby existiert bereits."); return; }
            var lobby = new Lobby(name, p);
            lobbies[name] = lobby;
            p.CurrentLobby = lobby;
            p.State = PlayerState.InLobby;
        }

        NetworkUtils.SafeSend(p, $"[SERVER] Lobby '{name}' erstellt. Warte auf Gegner");
        Log.Info($"{p.Username} hat Lobby '{name}' erstellt");
    }

    static void JoinLobby(Player p, string name)
    {
        lock (lobbiesLock)
        {
            if (!lobbies.ContainsKey(name)) { NetworkUtils.SafeSend(p, "[ERROR] Lobby existiert nicht."); return; }
            var lobby = lobbies[name];
            if (!lobby.IsOpen) { NetworkUtils.SafeSend(p, "[ERROR] Lobby ist voll."); return; }
            lobby.Opponent = p;
            p.CurrentLobby = lobby;
            p.State = PlayerState.InLobby;
            NetworkUtils.SafeSend(p, $"[SERVER] Du bist Lobby '{name}' beigetreten.");
            lobby.Broadcast($"{p.Username} ist der Lobby beigetreten.");
        }
        Log.Info($"{p.Username} ist Lobby '{name}' beigetreten.");
    }

    static void ListLobbies(Player p)
    {
        lock (lobbiesLock)
        {
            bool found = false;
            foreach (var l in lobbies.Values)
            {
                if (l.IsOpen)
                {
                    found = true;
                        NetworkUtils.SafeSend(p, $"{l.Name} (Host: {l.Host.Username})");
                }
            }
            if (!found) NetworkUtils.SafeSend(p, "[SERVER] Keine offenen Lobbys.");
        }
        NetworkUtils.SafeSend(p, "[SERVER] Ende der Liste.");
    }

    static void LeaveLobby(Player p)
    {
        var lobby = p.CurrentLobby;
        if (lobby == null) { NetworkUtils.SafeSend(p, "[ERROR] Du bist in keiner Lobby."); return; }

        bool inDuel = lobby.GameStarted;

        if (p == lobby.Host)
        {
            if (inDuel)
            {
                // Host verlässt Duell: beide raus, Lobby löschen
                if (lobby.Opponent != null)
                {
                    NetworkUtils.SafeSend(lobby.Opponent, "[SERVER] Host hat das Duell verlassen. Du wirst zurükgesetzt.");
                    lobby.Opponent.CurrentLobby = null;
                    lobby.Opponent.State = PlayerState.NotInLobby;
                }
                p.State = PlayerState.NotInLobby;
                p.CurrentLobby = null;

                lock (lobbiesLock) { lobbies.Remove(lobby.Name); }
                Log.Info($"Lobby '{lobby.Name}' gelöscht, Host hat Duell verlassen.");
            }
            else
            {
                // Lobby ohne laufendes Spiel
                if (lobby.Opponent != null)
                {
                    NetworkUtils.SafeSend(lobby.Opponent, "[SERVER] Host hat die Lobby verlassen. Du wirst gekickt.");
                    lobby.Opponent.CurrentLobby = null;
                    lobby.Opponent.State = PlayerState.NotInLobby;
                }
                p.State = PlayerState.NotInLobby;
                p.CurrentLobby = null;

                lock (lobbiesLock) { lobbies.Remove(lobby.Name); }
                Log.Info($"Lobby '{lobby.Name}' gelÃ¶scht, Host hat verlassen.");
            }
        }
        else if (p == lobby.Opponent)
        {
            lobby.Opponent = null;
            lobby.OpponentReady = false;

            if (inDuel)
            {
                // Gegner verlässt Duell: Host zurück in Lobby
                if (lobby.Host != null)
                {
                    lobby.Host.State = PlayerState.InLobby;
                    NetworkUtils.SafeSend(lobby.Host, "[SERVER] Gegner hat das Duell verlassen. Du bist zurÃ¼ck in der Lobby.");
                }
                Log.Info($"Duell in Lobby '{lobby.Name}' abgebrochen, Gegner hat verlassen.");
                lobby.GameStarted = false;
            }
            else
            {
                if (lobby.Host != null)
                    NetworkUtils.SafeSend(lobby.Host, "[SERVER] Gegner hat die Lobby verlassen.");
                Log.Info($"Lobby '{lobby.Name}': Gegner hat verlassen.");
            }
        }

        p.CurrentLobby = null;
        p.State = PlayerState.NotInLobby;
        lobby.ResetReady();
        NetworkUtils.SafeSend(p, "[SERVER] Du hast die Lobby verlassen.");

    }

    // ------------------------
    // Cards / Order / Ready / Start
    // ------------------------
    static void ShowCards(Player p)
    {
        var lobby = p.CurrentLobby;
        if (lobby == null) { NetworkUtils.SafeSend(p, "[ERROR] Du bist in keiner Lobby."); return; }
        int[] order = p == lobby.Host ? lobby.HostOrder : lobby.OpponentOrder;

        NetworkUtils.SafeSend(p, "[SERVER] Deine Karten in Reihenfolge:");
        foreach (int i in order)
        {
            var c = Lobby.Cards[i];
            NetworkUtils.SafeSend(p, $"{c.Name}: Angriff {c.MinAttack}-{c.MaxAttack}, Verteidigung {c.Defense}, Bonus {c.Bonus}");
        }
    }

    static void SetOrder(Player p, string arg)
    {
        var lobby = p.CurrentLobby;
        if (lobby == null) { NetworkUtils.SafeSend(p, "[ERROR] Du bist in keiner Lobby."); return; }

        string[] parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) { NetworkUtils.SafeSend(p, "[ERROR] Bitte 3 Karten angeben, z.B.: /order A B C"); return; }

        // PrÃ¼fen auf doppelte Karten
        if (parts[0].Equals(parts[1], StringComparison.OrdinalIgnoreCase) ||
            parts[0].Equals(parts[2], StringComparison.OrdinalIgnoreCase) ||
            parts[1].Equals(parts[2], StringComparison.OrdinalIgnoreCase))
        {
            NetworkUtils.SafeSend(p, "[ERROR] Jede Karte darf nur einmal vorkommen!");
            return;
        }

        int[] order = new int[3];
        for (int i = 0; i < 3; i++)
        {
            int idx = Array.FindIndex(Lobby.Cards, c => c.Name.Equals(parts[i], StringComparison.OrdinalIgnoreCase));
            if (idx < 0) { NetworkUtils.SafeSend(p, $"[ERROR] Karte {parts[i]} existiert nicht."); return; }
            order[i] = idx;
        }

        if (p == lobby.Host) lobby.HostOrder = order;
        else lobby.OpponentOrder = order;

        NetworkUtils.SafeSend(p, "[SERVER] Reihenfolge gesetzt: " + string.Join(" ", parts));
    }


    static void ToggleReady(Player p)
    {
        var lobby = p.CurrentLobby;
        if (lobby == null) return;

        if (p == lobby.Host) lobby.HostReady = !lobby.HostReady;
        else lobby.OpponentReady = !lobby.OpponentReady;

        NetworkUtils.SafeSend(p, $"[SERVER] Ready gesetzt: {(p == lobby.Host ? lobby.HostReady : lobby.OpponentReady)}");
        lobby.Broadcast($"{p.Username} ist ready: {(p == lobby.Host ? lobby.HostReady : lobby.OpponentReady)}");
    }

    static void StartGame(Player p)
    {
        var lobby = p.CurrentLobby;
        if (lobby == null) { NetworkUtils.SafeSend(p, "[ERROR] Lobby existiert nicht."); return; }
        if (p != lobby.Host) { NetworkUtils.SafeSend(p, "[ERROR] Nur Host kann starten."); return; }
        if (lobby.Opponent == null) { NetworkUtils.SafeSend(p, "[ERROR] Kein Gegner in Lobby."); return; }

        // PrÃ¼fen, ob das Duell bereits abgebrochen wurde
        if (lobby.GameStarted)
        {
            NetworkUtils.SafeSend(p, "[ERROR] Das Duell läuft bereits oder wurde gestartet.");
            return;
        }

        // PrÃ¼fen, ob beide Spieler ready sind
        if (!lobby.HostReady || !lobby.OpponentReady)
        {
            NetworkUtils.SafeSend(p, "[ERROR] Beide Spieler müssen ready sein, bevor das Duell gestartet werden kann.");
            return;
        }

        lobby.Duel = new DuelState();

        // Würfeln wer anfängt:
        Random rnd = new Random();
        bool hostStarts = rnd.Next(0, 2) == 0;

        if (hostStarts)
        {
            lobby.Duel.Attacker = lobby.Host;
            lobby.Duel.Defender = lobby.Opponent;
        }
        else
        {
            lobby.Duel.Attacker = lobby.Opponent;
            lobby.Duel.Defender = lobby.Host;
        }

        lobby.Broadcast("[SERVER] Das Duell beginnt!");
        lobby.Broadcast($"[SERVER] {lobby.Duel.Attacker.Username} greift zuerst an. Nutze /attack <Zahl>.");


        lobby.Host.State = PlayerState.InDuel;
        lobby.Opponent.State = PlayerState.InDuel;

        Log.Info($"Duell in Lobby '{lobby.Name}' gestartet zwischen {lobby.Host.Username} und {lobby.Opponent.Username}.");

    }



    // ------------------------
    // Global Chat
    // ------------------------
    static void BroadcastGlobalChat(Player p, string msg)
    {
        lock (playersLock)
        {
            foreach (var pl in players.Values)
            {
                if (pl.State == PlayerState.NotInLobby)
                    NetworkUtils.SafeSend(pl, $"{p.Username} (global): {msg}");
            }
        }
    }


    static void Disconnect(Player p)
    {
        if (p == null) return;
        LeaveLobby(p);

        lock (playersLock) players.Remove(p.Client);
        try { p.Stream?.Close(); p.Client?.Close(); }
        catch { }

        Log.Info($"Client getrennt: {p.Username ?? "Unknown"}");
    }

}