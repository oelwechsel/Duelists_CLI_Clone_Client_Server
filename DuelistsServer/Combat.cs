using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuelistsServer
{
    internal class Combat
    {
        public static void ExecuteAttack(Player p, Lobby lobby, string arg)
        {
            var d = lobby.Duel;

            if (p != d.Attacker)
            {
                NetworkUtils.SafeSend(p, "[ERROR] Du bist nicht am Zug zu angreifen.");
                return;
            }

            if (!d.WaitingForAttack)
            {
                NetworkUtils.SafeSend(p, "[ERROR] Angriff ist aktuell nicht möglich.");
                return;
            }

            if (!int.TryParse(arg, out int atkValue))
            {
                NetworkUtils.SafeSend(p, "[ERROR] Bitte gültige Zahl eingeben.");
                return;
            }

            int cardIndex = (p == lobby.Host ? lobby.HostOrder : lobby.OpponentOrder)[d.CurrentRound - 1];
            Card card = Lobby.Cards[cardIndex];

            if (atkValue < card.MinAttack || atkValue > card.MaxAttack)
            {
                NetworkUtils.SafeSend(p, "[ERROR] Angriffswert außerhalb deiner Range.");
                return;
            }

            d.LastAttackValue = atkValue;
            d.WaitingForAttack = false;
            d.WaitingForDefense = true;

            Log.BroadcastBox(lobby, "ANGRIFF",
                $"{p.Username} greift an!",
                $"Angriffswert: ?"
            );

            NetworkUtils.SafeSend(d.Defender, "[SERVER] Du bist dran zu verteidigen: /defend <Zahlen>");
        }



        public static void ExecuteDefense(Player p, Lobby lobby, string arg)
        {
            var d = lobby.Duel;

            if (p != d.Defender)
            {
                NetworkUtils.SafeSend(p, "[ERROR] Du bist nicht am Zug zu verteidigen.");
                return;
            }

            if (!d.WaitingForDefense)
            {
                NetworkUtils.SafeSend(p, "[ERROR] Verteidigung ist gerade nicht möglich.");
                return;
            }

            string[] parts = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            List<int> guesses = new();

            foreach (var s in parts)
                if (!int.TryParse(s, out int num))
                {
                    NetworkUtils.SafeSend(p, "[ERROR] Ungültige Eingabe.");
                    return;
                }
                else
                    guesses.Add(num);

            // Karte des Verteidigers
            int cardIndex = (p == lobby.Host ? lobby.HostOrder : lobby.OpponentOrder)[d.CurrentRound - 1];
            Card card = Lobby.Cards[cardIndex];

            if (guesses.Count != card.Defense)
            {
                NetworkUtils.SafeSend(p, $"[ERROR] Deine Karte erlaubt {card.Defense} Verteidigungsversuche.");
                return;
            }

            bool hit = true;
            foreach (int guess in guesses)
                if (guess == d.LastAttackValue)
                    hit = false;

            if (hit)
            {
                // Karte des Angreifers
                Player attacker = d.Attacker;
                int attackerCardIndex = (attacker == lobby.Host ? lobby.HostOrder : lobby.OpponentOrder)[d.CurrentRound - 1];
                Card attackerCard = Lobby.Cards[attackerCardIndex];

                int bonus = attackerCard.Bonus;
                int damage = d.LastAttackValue + bonus;

                if (d.Attacker == lobby.Host)
                    d.OppHP -= damage;
                else
                    d.HostHP -= damage;

                Log.BroadcastBox(lobby, "TREFFER!",
                    $"{p.Username} konnte nicht ausweichen!",
                    $"Schaden: {d.LastAttackValue} + Bonus {bonus}"
                );
            }

            else
            {
                Log.BroadcastBox(lobby, "VERTEIDIGUNG",
                    $"{p.Username} verteidigt!",
                    "Ergebnis: Erfolgreich – kein Schaden!"
                );

            }

            // Verteidigung abgeschlossen
            d.WaitingForDefense = false;

            // Wichtig: pro Karte exakt 2 Attacken
            d.AttacksDoneThisCard++;

            // Rollen tauschen
            SwapRoles(lobby);

            Log.ShowRoundSummary(lobby);

            // Weiter
            NextDuelStep(lobby);
        }







        static void SwapRoles(Lobby lobby)
        {
            var d = lobby.Duel;

            var temp = d.Attacker;
            d.Attacker = d.Defender;
            d.Defender = temp;

            d.WaitingForAttack = true;
        }

        static void NextDuelStep(Lobby lobby)
        {
            var d = lobby.Duel;

            // Sieg?
            if (d.HostHP <= 0 || d.OppHP <= 0)
            {
                string winner = d.HostHP <= 0 ? lobby.Opponent.Username : lobby.Host.Username;

                lobby.Broadcast($"[SERVER] DAS DUELL IST VORBEI! Gewinner: {winner}");

                // Spieler zurück in Lobby State
                lobby.Host.State = PlayerState.InLobby;
                lobby.Opponent.State = PlayerState.InLobby;

                lobby.GameStarted = false;

                // Reset Ready
                lobby.HostReady = false;
                lobby.OpponentReady = false;

                // DuelState entfernen
                lobby.Duel = null;

                return;
            }


            // Karte fertig? (nach 2 Attack/Defense-Zyklen)
            if (d.AttacksDoneThisCard >= 2)
            {
                d.CurrentRound++;
                d.AttacksDoneThisCard = 0;

                if (d.AttacksDoneThisCard >= 2)
                {
                    d.CurrentRound++;

                    if (d.CurrentRound > 3)
                        d.CurrentRound = 1;   // Wieder zu Karte 1

                    d.AttacksDoneThisCard = 0;

                    lobby.Broadcast($"[SERVER] ----> Nächste Karte! Karte {d.CurrentRound}");
                }


                lobby.Broadcast($"[SERVER] ----> Nächste Karte! Karte {d.CurrentRound}/3");
            }

            // Nächster Angriff
            NetworkUtils.SafeSend(d.Attacker, "[SERVER] Du bist am Zug: /attack <Zahl>");
        }
    }
}
