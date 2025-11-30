using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DuelistsServer
{
    enum PlayerState { NotInLobby, InLobby, InDuel }
    internal class Player
    {
        public TcpClient Client;
        public NetworkStream Stream;
        public string Username;
        public PlayerState State = PlayerState.NotInLobby;
        public Lobby CurrentLobby = null;
        public string IPAddress { get; set; }
        public int Port { get; set; }
    }
}
