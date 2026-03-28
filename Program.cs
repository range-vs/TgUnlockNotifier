using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Renci.SshNet;

class Program
{
    static Dictionary<string, string> ParseArgs(string[] args)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith("--"))
            {
                string key = args[i].Substring(2);

                if (key == "help")
                {
                    dict["help"] = "true";
                    continue;
                }

                if (i + 1 < args.Length)
                {
                    dict[key] = args[i + 1];
                    i++;
                }
            }
        }

        return dict;
    }

    static string GetRequired(Dictionary<string, string> dict, string key)
    {
        if (!dict.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            Console.WriteLine($"Error: arg not found --{key}\n");
            PrintHelp();
            Environment.Exit(1);
        }

        return value;
    }

    static void PrintHelp()
    {
        Console.WriteLine("Using:");
        Console.WriteLine("  app.exe --sshHost <host> --sshUser <user> --sshPass <pass> \\");
        Console.WriteLine("          --botToken <token> --chatId <id> --message <text>\n");

        Console.WriteLine("Аргументы:");
        Console.WriteLine("  --sshHost     IP or domain server");
        Console.WriteLine("  --sshUser     SSH username");
        Console.WriteLine("  --sshPass     SSH password");
        Console.WriteLine("  --botToken    Telegram bot token");
        Console.WriteLine("  --chatId      Telegram chat id");
        Console.WriteLine("  --message     Telegram message");
        Console.WriteLine("  --help        Show help");
    }

    static async Task Main(string[] args)
    {
        int localPort = 1080;

        var parsed = ParseArgs(args);

        if (parsed.ContainsKey("help") || args.Length == 0)
        {
            PrintHelp();
            return;
        }

        string sshHost = GetRequired(parsed, "sshHost");
        string sshUser = GetRequired(parsed, "sshUser");
        string sshPass = GetRequired(parsed, "sshPass");

        string botToken = GetRequired(parsed, "botToken");
        string chatId = GetRequired(parsed, "chatId");
        string message = GetRequired(parsed, "message");

        using var client = new SshClient(sshHost, sshUser, sshPass);

        try
        {
            Console.WriteLine("Connect to SSH...");
            client.Connect();

            var proxy = new ForwardedPortDynamic("127.0.0.1", (uint)localPort);
            client.AddForwardedPort(proxy);
            proxy.Start();

            Console.WriteLine($"SOCKS5 tunnel active on port {localPort}");

            var proxySettings = new WebProxy
            {
                Address = new Uri($"socks5://127.0.0.1:{localPort}")
            };

            var handler = new HttpClientHandler { Proxy = proxySettings };
            using var httpClient = new HttpClient(handler);

            string url = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={Uri.EscapeDataString(message)}";

            Console.WriteLine("Send message to Telegram...");
            var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
                Console.WriteLine("Message sended!");
            else
                Console.WriteLine($"Error: {response.StatusCode}");

            proxy.Stop();
            client.Disconnect();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }

    }
}