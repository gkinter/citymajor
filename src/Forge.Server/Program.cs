using Forge.Engine.Core;
using Forge.Engine.Network;
using Forge.Engine.Simulation;

namespace Forge.Server;

/// <summary>
/// Headless multiplayer server. Runs the simulation without rendering,
/// accepts player connections via ENet, and broadcasts state updates.
/// </summary>
public static class Program
{
    public static void Main(string[] args)
    {
        int port = 7777;
        int worldSize = 512;

        // Parse command-line args
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out port);
            if (args[i] == "--world-size" && i + 1 < args.Length)
                int.TryParse(args[i + 1], out worldSize);
        }

        Console.WriteLine($"[Server] Starting on port {port}, world size {worldSize}x{worldSize}");

        var config = new Config
        {
            WorldSize = worldSize,
            NetworkPort = port,
        };

        var simLoop = new SimulationLoop(config);
        var network = new NetworkManager();

        network.StartServer((ushort)port);
        simLoop.Start();

        network.OnClientConnected += clientId =>
        {
            Console.WriteLine($"[Server] Player {clientId} joined.");
        };

        network.OnClientDisconnected += clientId =>
        {
            Console.WriteLine($"[Server] Player {clientId} left.");
        };

        network.OnPacketReceived += (clientId, packet) =>
        {
            switch (packet)
            {
                case PlayerCommandPacket cmd:
                    simLoop.Commands.TryEnqueue(new CommandQueue.Command
                    {
                        Type = (CommandQueue.CommandType)cmd.CommandType,
                        X = cmd.X,
                        Y = cmd.Y,
                        Width = cmd.Width,
                        Height = cmd.Height,
                        DataId = cmd.DataId,
                        DataValue = cmd.DataValue,
                    });
                    break;

                case ChatMessagePacket chat:
                    // Relay chat to all clients.
                    network.Broadcast(chat);
                    break;
            }
        };

        Console.WriteLine("[Server] Running. Press Ctrl+C to stop.");
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            simLoop.Stop();
            network.Dispose();
            Console.WriteLine("[Server] Stopped.");
            Environment.Exit(0);
        };

        // Main loop: poll network events
        while (true)
        {
            network.PollEvents();
            Thread.Sleep(1);
        }
    }
}
