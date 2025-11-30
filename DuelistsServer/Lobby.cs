using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuelistsServer
{
    internal class Lobby
    {
        public string Name;
        public Player Host;
        public Player Opponent = null;

        public DuelState Duel = null;
        public bool IsOpen => Opponent == null;

        public bool HostReady = false;
        public bool OpponentReady = false;

        public int[] HostOrder = new int[3] { 0, 1, 2 };
        public int[] OpponentOrder = new int[3] { 0, 1, 2 };

        public bool GameStarted = false;

        public static readonly Card[] Cards = new Card[]
        {
        new Card("A", 1, 5, 1, 0),
        new Card("B", 1, 4, 2, 1),
        new Card("C", 1, 2, 3, 0)
        };

        public Lobby(string name, Player host)
        {
            Name = name;
            Host = host;
        }

        public void Broadcast(string msg)
        {
            if (Host != null) NetworkUtils.SafeSend(Host, msg);
            if (Opponent != null) NetworkUtils.SafeSend(Opponent, msg);
        }

        public void ResetReady()
        {
            HostReady = false;
            OpponentReady = false;
        }
    }
}
