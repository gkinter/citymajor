using ENet;

namespace Forge.Engine.Network;

/// <summary>
/// Tracks network performance metrics: ping, throughput, packet loss.
/// Updated each frame from ENet peer statistics.
/// </summary>
public sealed class NetworkStats
{
    /// <summary>Round-trip time in milliseconds (smoothed).</summary>
    public int PingMs { get; internal set; }

    /// <summary>Bytes received per second (rolling average).</summary>
    public long BytesReceivedPerSec { get; internal set; }

    /// <summary>Bytes sent per second (rolling average).</summary>
    public long BytesSentPerSec { get; internal set; }

    /// <summary>Estimated packet loss ratio (0.0 = none, 1.0 = total loss).</summary>
    public float PacketLossRatio { get; internal set; }

    /// <summary>Total packets received since connection.</summary>
    public long TotalPacketsReceived { get; internal set; }

    /// <summary>Total packets sent since connection.</summary>
    public long TotalPacketsSent { get; internal set; }

    internal long _bytesReceivedWindow;
    internal long _bytesSentWindow;
    internal long _lastStatsUpdateMs;
}

/// <summary>
/// ENet-based client-server networking layer for multiplayer city collaboration.
///
/// Uses two ENet channels:
///   Channel 0 -- reliable ordered (game state, commands, trade)
///   Channel 1 -- unreliable unordered (heartbeat, position updates)
///
/// Supports:
///   - Server hosting with configurable max clients
///   - Client connection with automatic reconnection attempts
///   - Typed packet send/receive via IPacket + PacketRegistry
///   - Connection lifecycle events (connect, disconnect, timeout)
///   - Network statistics (ping, throughput, packet loss)
/// </summary>
public sealed class NetworkManager : IDisposable
{
    private const int ChannelCount = 2;
    private const int ReliableChannel = 0;
    private const int UnreliableChannel = 1;
    private const int MaxReconnectAttempts = 5;
    private const int ReconnectDelayMs = 2000;
    private const int TimeoutMs = 10000;

    private Host? _host;
    private Peer _serverPeer;
    private bool _isServer;
    private bool _isClient;
    private bool _initialized;
    private bool _disposed;

    // Reconnection state (client only).
    private string _lastHost = string.Empty;
    private ushort _lastPort;
    private int _reconnectAttempts;
    private long _nextReconnectTimeMs;
    private bool _wasConnected;

    private readonly Dictionary<int, Peer> _connectedPeers = new();
    private readonly NetworkStats _stats = new();
    private int _maxClients;

    // ---------- public properties ----------

    public bool IsServer => _isServer;
    public bool IsClient => _isClient;

    /// <summary>True if this client is connected to a server, or if the server is running.</summary>
    public bool IsConnected => _initialized && (_isServer || _wasConnected);

    public int ConnectedClientCount => _connectedPeers.Count;
    public NetworkStats Stats => _stats;

    // ---------- events ----------

    /// <summary>Raised on the server when a new client connects. Parameter is the client ID.</summary>
    public event Action<int>? OnClientConnected;

    /// <summary>Raised on the server when a client disconnects or times out. Parameter is the client ID.</summary>
    public event Action<int>? OnClientDisconnected;

    /// <summary>Raised when a typed packet is received. Parameters: (senderClientId, packet).</summary>
    public event Action<int, IPacket>? OnPacketReceived;

    // ---------- server mode ----------

    /// <summary>
    /// Start a server listening on the given port.
    /// </summary>
    public void StartServer(ushort port, int maxClients = 8)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(NetworkManager));
        if (_initialized) throw new InvalidOperationException("NetworkManager already initialized.");

        Library.Initialize();

        _host = new Host();
        var address = new Address { Port = port };
        _host.Create(address, maxClients, ChannelCount);

        _isServer = true;
        _isClient = false;
        _initialized = true;
        _maxClients = maxClients;

        Console.WriteLine($"[NetworkManager] Server started on port {port}, max clients: {maxClients}");
    }

    /// <summary>Stop the server and disconnect all clients.</summary>
    public void StopServer()
    {
        if (!_isServer || !_initialized) return;

        foreach (var (_, peer) in _connectedPeers)
            peer.DisconnectNow(0);

        _connectedPeers.Clear();
        _host?.Flush();
        _host?.Dispose();
        _host = null;
        _isServer = false;
        _initialized = false;

        Library.Deinitialize();
        Console.WriteLine("[NetworkManager] Server stopped.");
    }

    // ---------- client mode ----------

    /// <summary>
    /// Connect to a server at the given host and port.
    /// </summary>
    public void Connect(string host, ushort port)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(NetworkManager));
        if (_initialized) throw new InvalidOperationException("NetworkManager already initialized.");

        Library.Initialize();

        _host = new Host();
        _host.Create();

        var address = new Address();
        address.SetHost(host);
        address.Port = port;

        _serverPeer = _host.Connect(address, ChannelCount);
        _serverPeer.Timeout(0, TimeoutMs, TimeoutMs);

        _isClient = true;
        _isServer = false;
        _initialized = true;
        _lastHost = host;
        _lastPort = port;
        _reconnectAttempts = 0;

        Console.WriteLine($"[NetworkManager] Connecting to {host}:{port}...");
    }

    /// <summary>Gracefully disconnect from the server.</summary>
    public void Disconnect()
    {
        if (!_initialized) return;

        if (_isClient)
        {
            _serverPeer.DisconnectNow(0);
            _wasConnected = false;
            _reconnectAttempts = MaxReconnectAttempts; // Prevent auto-reconnect.
        }

        _host?.Flush();
        _host?.Dispose();
        _host = null;
        _initialized = false;
        _isClient = false;
        _isServer = false;

        Library.Deinitialize();
    }

    // ---------- send ----------

    /// <summary>Send a packet from client to server.</summary>
    public void SendToServer(IPacket packet, bool reliable = true)
    {
        if (!_isClient || !_initialized || _host == null) return;

        byte[] data = PacketRegistry.Serialize(packet);
        _stats.TotalPacketsSent++;
        _stats._bytesSentWindow += data.Length;

        var enetPacket = default(Packet);
        byte channel = reliable ? (byte)ReliableChannel : (byte)UnreliableChannel;
        enetPacket.Create(data, reliable ? PacketFlags.Reliable : PacketFlags.Unsequenced);
        _serverPeer.Send(channel, ref enetPacket);
    }

    /// <summary>Send a packet from server to a specific client.</summary>
    public void SendToClient(int clientId, IPacket packet, bool reliable = true)
    {
        if (!_isServer || !_initialized || _host == null) return;
        if (!_connectedPeers.TryGetValue(clientId, out var peer)) return;

        byte[] data = PacketRegistry.Serialize(packet);
        _stats.TotalPacketsSent++;
        _stats._bytesSentWindow += data.Length;

        var enetPacket = default(Packet);
        byte channel = reliable ? (byte)ReliableChannel : (byte)UnreliableChannel;
        enetPacket.Create(data, reliable ? PacketFlags.Reliable : PacketFlags.Unsequenced);
        peer.Send(channel, ref enetPacket);
    }

    /// <summary>Broadcast a packet to all connected clients (server only).</summary>
    public void Broadcast(IPacket packet, bool reliable = true)
    {
        if (!_isServer || !_initialized || _host == null) return;

        byte[] data = PacketRegistry.Serialize(packet);
        _stats.TotalPacketsSent++;
        _stats._bytesSentWindow += data.Length;

        var enetPacket = default(Packet);
        byte channel = reliable ? (byte)ReliableChannel : (byte)UnreliableChannel;
        enetPacket.Create(data, reliable ? PacketFlags.Reliable : PacketFlags.Unsequenced);
        _host.Broadcast(channel, ref enetPacket);
    }

    // ---------- polling ----------

    /// <summary>
    /// Process all pending network events. Call once per frame from the main thread.
    /// Fires OnClientConnected, OnClientDisconnected, and OnPacketReceived events.
    /// </summary>
    public void PollEvents()
    {
        if (!_initialized || _host == null) return;

        // Handle reconnection for client mode.
        if (_isClient && !_wasConnected && _reconnectAttempts > 0 && _reconnectAttempts < MaxReconnectAttempts)
        {
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (nowMs >= _nextReconnectTimeMs)
            {
                AttemptReconnect();
            }
        }

        Event netEvent;
        bool polled = false;

        while (!polled)
        {
            if (_host.CheckEvents(out netEvent) <= 0)
            {
                if (_host.Service(0, out netEvent) <= 0)
                    break;
                polled = true;
            }

            switch (netEvent.Type)
            {
                case EventType.Connect:
                    HandleConnect(netEvent);
                    break;

                case EventType.Disconnect:
                    HandleDisconnect(netEvent);
                    break;

                case EventType.Receive:
                    HandleReceive(netEvent);
                    break;

                case EventType.Timeout:
                    HandleTimeout(netEvent);
                    break;
            }
        }

        UpdateStats();
    }

    // ---------- event handlers ----------

    private void HandleConnect(Event e)
    {
        int peerId = (int)e.Peer.ID;

        if (_isServer)
        {
            _connectedPeers[peerId] = e.Peer;
            e.Peer.Timeout(0, TimeoutMs, TimeoutMs);
            Console.WriteLine($"[NetworkManager] Client {peerId} connected. Total: {_connectedPeers.Count}");
            OnClientConnected?.Invoke(peerId);
        }
        else
        {
            _wasConnected = true;
            _reconnectAttempts = 0;
            Console.WriteLine("[NetworkManager] Connected to server.");
        }
    }

    private void HandleDisconnect(Event e)
    {
        int peerId = (int)e.Peer.ID;

        if (_isServer)
        {
            _connectedPeers.Remove(peerId);
            Console.WriteLine($"[NetworkManager] Client {peerId} disconnected. Total: {_connectedPeers.Count}");
            OnClientDisconnected?.Invoke(peerId);
        }
        else
        {
            _wasConnected = false;
            Console.WriteLine("[NetworkManager] Disconnected from server.");
            ScheduleReconnect();
        }
    }

    private void HandleReceive(Event e)
    {
        int peerId = (int)e.Peer.ID;

        byte[] data = new byte[e.Packet.Length];
        e.Packet.CopyTo(data);
        e.Packet.Dispose();

        _stats.TotalPacketsReceived++;
        _stats._bytesReceivedWindow += data.Length;

        IPacket? packet = PacketRegistry.Deserialize(data);
        if (packet != null)
        {
            OnPacketReceived?.Invoke(peerId, packet);
        }
    }

    private void HandleTimeout(Event e)
    {
        int peerId = (int)e.Peer.ID;

        if (_isServer)
        {
            _connectedPeers.Remove(peerId);
            Console.WriteLine($"[NetworkManager] Client {peerId} timed out. Total: {_connectedPeers.Count}");
            OnClientDisconnected?.Invoke(peerId);
        }
        else
        {
            _wasConnected = false;
            Console.WriteLine("[NetworkManager] Server connection timed out.");
            ScheduleReconnect();
        }
    }

    // ---------- reconnection ----------

    private void ScheduleReconnect()
    {
        if (_reconnectAttempts >= MaxReconnectAttempts) return;

        _reconnectAttempts++;
        _nextReconnectTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ReconnectDelayMs;
        Console.WriteLine($"[NetworkManager] Reconnect attempt {_reconnectAttempts}/{MaxReconnectAttempts} in {ReconnectDelayMs}ms...");
    }

    private void AttemptReconnect()
    {
        if (_host == null || string.IsNullOrEmpty(_lastHost)) return;

        var address = new Address();
        address.SetHost(_lastHost);
        address.Port = _lastPort;

        _serverPeer = _host.Connect(address, ChannelCount);
        _serverPeer.Timeout(0, TimeoutMs, TimeoutMs);

        Console.WriteLine($"[NetworkManager] Reconnecting to {_lastHost}:{_lastPort} (attempt {_reconnectAttempts})...");
    }

    // ---------- stats ----------

    private void UpdateStats()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long elapsed = nowMs - _stats._lastStatsUpdateMs;

        if (elapsed >= 1000)
        {
            _stats.BytesReceivedPerSec = _stats._bytesReceivedWindow * 1000 / elapsed;
            _stats.BytesSentPerSec = _stats._bytesSentWindow * 1000 / elapsed;
            _stats._bytesReceivedWindow = 0;
            _stats._bytesSentWindow = 0;
            _stats._lastStatsUpdateMs = nowMs;
        }

        // Update ping from server peer (client mode) or average across clients (server mode).
        if (_isClient && _wasConnected)
        {
            _stats.PingMs = (int)_serverPeer.RoundTripTime;
            _stats.PacketLossRatio = _serverPeer.PacketsSent > 0
                ? (float)_serverPeer.PacketsLost / _serverPeer.PacketsSent
                : 0f;
        }
        else if (_isServer && _connectedPeers.Count > 0)
        {
            long totalRtt = 0;
            float totalLoss = 0;
            foreach (var (_, peer) in _connectedPeers)
            {
                totalRtt += peer.RoundTripTime;
                totalLoss += peer.PacketsSent > 0
                    ? (float)peer.PacketsLost / peer.PacketsSent
                    : 0f;
            }
            _stats.PingMs = (int)(totalRtt / _connectedPeers.Count);
            _stats.PacketLossRatio = totalLoss / _connectedPeers.Count;
        }
    }

    // ---------- dispose ----------

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_isServer)
            StopServer();
        else
            Disconnect();
    }
}
