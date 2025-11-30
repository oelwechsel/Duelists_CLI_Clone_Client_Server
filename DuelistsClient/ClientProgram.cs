using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class ClientProgram
{
    private static volatile bool running = true;
    private static TcpClient client;
    private static NetworkStream stream;
    private static string username = null;
    private static Thread receiveThread;
static void Main()
    {
        try
        {
            while (running && !ConnectToServer())
            {
                LogWarn("Bitte geben Sie eine gültige Server-IP ein.");
            }

            StartReceiveThread();
            PerformUsernameHandshake();
            CommandLoop();
        }
        catch (Exception ex)
        {
            LogError($"Unerwarteter Fehler: {ex.Message}");
        }
        finally
        {
            running = false;
            Cleanup();
        }
    }

    private static bool ConnectToServer()
    {
        Console.Write("Server IP eingeben: ");
        string serverIP = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(serverIP))
        {
            LogWarn("Server IP darf nicht leer sein.");
            return false;
        }

        try
        {
            client?.Close();
            client = new TcpClient();
            client.Connect(serverIP, 9000);
            stream = client.GetStream();
            LogWarn($"Verbunden mit {serverIP}:9000");
            return true;
        }
        catch (Exception ex)
        {
            LogError($"Konnte nicht verbinden: {ex.Message}");
            return false;
        }
    }

    private static void StartReceiveThread()
    {
        receiveThread = new Thread(() =>
        {
            byte[] buffer = new byte[2048];
            while (running)
            {
                try
                {
                    if (stream == null || !stream.CanRead)
                        break;

                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead <= 0)
                        break;

                    string msg = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    if (string.IsNullOrWhiteSpace(msg))
                        continue;

                    ColorLog(msg);

                    if (msg.Contains("Willkommen") && username == null)
                        ExtractUsername(msg);
                }
                catch
                {
                    break;
                }
            }

            if (running)
            {
                LogError("Verbindung wurde vom Server getrennt.");
                running = false;
            }
        })
        { IsBackground = true };
        receiveThread.Start();
    }

    private static void ExtractUsername(string msg)
    {
        try
        {
            int idx = msg.IndexOf(",") + 2;
            if (idx > 0 && idx < msg.Length)
                username = msg.Substring(idx).Trim('!', ' ');
        }
        catch { }
    }

    private static void PerformUsernameHandshake()
    {
        while (running && username == null)
        {
            Console.Write("Username eingeben: ");
            string name = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(name))
            {
                LogWarn("Username darf nicht leer sein.");
                continue;
            }

            SafeSend($"USERNAME|{name}\n");
            Thread.Sleep(250);
        }
    }

    private static void CommandLoop()
    {
        while (running)
        {
            string input;
            try
            {
                input = Console.ReadLine();
            }
            catch
            {
                break;
            }

            if (!running || string.IsNullOrWhiteSpace(input))
                continue;

            if (!input.StartsWith("/"))
            {
                LogError("Nur Commands erlaubt. Beispiel: /chat <Nachricht>");
                continue;
            }

            string cmd = input;
            string arg = "";
            int space = input.IndexOf(' ');
            if (space > 0)
            {
                cmd = input.Substring(0, space);
                arg = input.Substring(space + 1);
            }

            SafeSend($"{cmd}|{arg}\n");
        }
    }

    private static void SafeSend(string msg)
    {
        try
        {
            if (stream != null && stream.CanWrite && !string.IsNullOrEmpty(msg))
            {
                byte[] data = Encoding.UTF8.GetBytes(msg);
                stream.Write(data, 0, data.Length);
            }
        }
        catch (Exception ex)
        {
            LogError($"Nachricht konnte nicht gesendet werden: {ex.Message}");
            running = false;
        }
    }

    private static void Cleanup()
    {
        running = false;
        try { stream?.Close(); } catch { }
        try { client?.Close(); } catch { }
        try { receiveThread?.Join(500); } catch { }
        LogWarn("Client beendet.");
    }

    private static void ColorLog(string msg)
    {
        ConsoleColor color = msg.StartsWith("[SERVER]") ? ConsoleColor.Green :
                             msg.StartsWith("[ERROR]") ? ConsoleColor.Red :
                             msg.StartsWith("[WARN]") ? ConsoleColor.Yellow :
                             ConsoleColor.Cyan;
        Console.ForegroundColor = color;
        Console.WriteLine(msg);
        Console.ResetColor();
    }

    private static void LogError(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[ERROR] " + msg);
        Console.ResetColor();
    }

    private static void LogWarn(string msg)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("[WARN] " + msg);
        Console.ResetColor();
    }

}
