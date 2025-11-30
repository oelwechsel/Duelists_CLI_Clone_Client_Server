using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuelistsServer
{
    internal class DuelState
    {
        public Player Attacker;
        public Player Defender;

        public int HostHP = 14;
        public int OppHP = 14;

        public int CurrentRound = 1; // Karte 1 → 2 → 3

        public bool WaitingForAttack = true;
        public bool WaitingForDefense = false;

        public int LastAttackValue = 0;

        // Anzahl Attack-Vorgänge mit dieser Karte
        public int AttacksDoneThisCard = 0; // MUSS 2 erreichen

        public bool IsFinished => HostHP <= 0 || OppHP <= 0;
    }
}
