using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace DuelistsServer
{
    internal class NetworkUtils
    {
        // ------------------------
        // Low-level helpers
        // ------------------------
        public static int SafeRead(Player p, byte[] buffer)
        {
            if (p == null || p.Stream == null || p.Client == null) return -1;
            try
            {
                if (!p.Stream.DataAvailable) return 0;
                return p.Stream.Read(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                Log.Error($"Read-Fehler ({p.Username ?? p.IPAddress}): {ex.Message}");
                return -1;
            }
        }

        public static bool IsDisconnected(Player p)
        {
            if (p == null || p.Client == null) return true;
            try
            {
                Socket s = p.Client.Client;
                return !p.Client.Connected || (s.Poll(0, SelectMode.SelectRead) && s.Available == 0);
            }
            catch { return true; }
        }

        public static void SafeSend(Player p, string msg)
        {
            if (p == null) return;
            try
            {
                if (p.Stream != null && p.Client != null && p.Client.Connected)
                {
                    byte[] data = Encoding.UTF8.GetBytes(msg + "\n");
                    p.Stream.Write(data, 0, data.Length);
                }
                else
                {
                    Log.Warn($"Send abgebrochen (nicht verbunden): {p.Username ?? p.IPAddress}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Send-Fehler zu {p.Username ?? p.IPAddress}: {ex.Message}");
            }
        }
    }
}
