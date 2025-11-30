using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DuelistsServer
{
    enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3 }
    internal static class Log
    {

        private static readonly object _lock = new();
        public static LogLevel MinimumLevel = LogLevel.Info;

        public static void Info(string msg) => Write(LogLevel.Info, "INFO ", ConsoleColor.Green, msg);
        public static void Warn(string msg) => Write(LogLevel.Warn, "WARN ", ConsoleColor.Yellow, msg);
        public static void Error(string msg) => Write(LogLevel.Error, "ERROR", ConsoleColor.Red, msg);
        public static void Debug(string msg) => Write(LogLevel.Debug, "DEBUG", ConsoleColor.Gray, msg);

        private static void Write(LogLevel level, string label, ConsoleColor color, string msg)
        {
            if (level < MinimumLevel) return;
            lock (_lock)
            {
                Console.ForegroundColor = color;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} | {label.PadRight(5)} | {msg}");
                Console.ResetColor();
            }
        }


        public static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.DarkCyan;
            Console.WriteLine("============================================================");
            Console.WriteLine("                      DUEL SERVER v2.0                      ");
            Console.WriteLine("============================================================");
            Console.ResetColor();
        }

        public static void PrintLocalIPs()
        {
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    Log.Info($"Server-IP: {ip}");
        }

        public static void SendBox(Player p, string title, params string[] lines)
        {
            StringBuilder sb = new();
            sb.AppendLine("==========================================");
            sb.AppendLine($"=== {title}");
            sb.AppendLine("==========================================");

            foreach (var line in lines)
                sb.AppendLine(line);

            sb.AppendLine("==========================================");

            NetworkUtils.SafeSend(p, sb.ToString());
        }

        public static void SendBlock(Player p, params string[] lines)
        {
            StringBuilder sb = new();
            foreach (var line in lines)
                sb.AppendLine("> " + line);

            NetworkUtils.SafeSend(p, sb.ToString());
        }

        public static void BroadcastBox(Lobby lobby, string title, params string[] lines)
        {
            if (lobby.Host != null) SendBox(lobby.Host, title, lines);
            if (lobby.Opponent != null) SendBox(lobby.Opponent, title, lines);
        }

        public static void ShowHelp(Player p)
        {
            NetworkUtils.SafeSend(p, "[SERVER] /help - zeigt diese Hilfe");

            if (p.State == PlayerState.NotInLobby)
            {
                NetworkUtils.SafeSend(p, "[SERVER] /create <name> - Lobby erstellen");
                NetworkUtils.SafeSend(p, "[SERVER] /join <name> - Lobby beitreten");
                NetworkUtils.SafeSend(p, "[SERVER] /duels - offene Lobbys anzeigen");
                NetworkUtils.SafeSend(p, "[SERVER] /chat <msg> - Globalen Chat senden");
            }
            else if (p.State == PlayerState.InLobby)
            {
                NetworkUtils.SafeSend(p, "[SERVER] /leave - Lobby verlassen");
                NetworkUtils.SafeSend(p, "[SERVER] /cards - zeigt deine Karten");
                NetworkUtils.SafeSend(p, "[SERVER] /order <A B C> - Reihenfolge festlegen");
                NetworkUtils.SafeSend(p, "[SERVER] /ready - Ready toggeln");
                NetworkUtils.SafeSend(p, "[SERVER] /start - nur Host startet Duell");
                NetworkUtils.SafeSend(p, "[SERVER] /chat <msg> - Lobby-Chat senden");
            }
            else if (p.State == PlayerState.InDuel)
            {
                NetworkUtils.SafeSend(p, "[SERVER] /leave - Duell verlassen");
                NetworkUtils.SafeSend(p, "[SERVER] /chat <msg> - Duell-Chat senden");
                NetworkUtils.SafeSend(p, "[SERVER] /stats - Zeigt Stats auf -> HP -> Karten etc.");
                NetworkUtils.SafeSend(p, "[SERVER] /attack <value> - abhängig von angriffsrange");
                NetworkUtils.SafeSend(p, "[SERVER] /defend <value> <value> ... - abhängig von defense value");
            }
        }

        public static void ShowStats(Player p, Lobby lobby)
        {
            var d = lobby.Duel;

            Player enemy = p == lobby.Host ? lobby.Opponent : lobby.Host;

            int ownIndex = (p == lobby.Host ? lobby.HostOrder : lobby.OpponentOrder)[d.CurrentRound - 1];
            int enemyIndex = (enemy == lobby.Host ? lobby.HostOrder : lobby.OpponentOrder)[d.CurrentRound - 1];

            Card own = Lobby.Cards[ownIndex];
            Card enemyCard = Lobby.Cards[enemyIndex];

            Log.SendBox(p, "DEINE STATISTIKEN",
                $"HP: Du = {(p == lobby.Host ? d.HostHP : d.OppHP)}, Gegner = {(p == lobby.Host ? d.OppHP : d.HostHP)}",
                $"Runde: {d.CurrentRound} / 3",
                "",
                $"Deine Karte:",
                $"{own.Name}  | Atk {own.MinAttack}-{own.MaxAttack} | Def {own.Defense} | Bonus {own.Bonus}",
                "",
                $"Gegnerische Karte:",
                $"{enemyCard.Name} | Atk {enemyCard.MinAttack}-{enemyCard.MaxAttack} | Def {enemyCard.Defense} | Bonus {enemyCard.Bonus}"
            );
        }

        public static void ShowRoundSummary(Lobby lobby)
        {
            var d = lobby.Duel;

            string attackerName = d.Defender == lobby.Host ? lobby.Opponent.Username : lobby.Host.Username;
            string defenderName = d.Attacker == lobby.Host ? lobby.Opponent.Username : lobby.Host.Username;

            Log.BroadcastBox(lobby,
                "Zwischenbilanz der Runde",
                $"Angriffswert: {d.LastAttackValue}",
                $"HP Host ({lobby.Host.Username}): {d.HostHP}",
                $"HP Gegner ({lobby.Opponent.Username}): {d.OppHP}",
                "",
                $"Nächster Angreifer: {d.Attacker.Username}"
            );
        }
    }
}
