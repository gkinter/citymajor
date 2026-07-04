namespace Forge.Engine.Network;

/// <summary>
/// Steam networking wrapper for NAT traversal and Steam relay servers.
/// Provides lobby creation/joining and relay-based data transport using
/// Steamworks.NET. Falls back to direct ENet when Steam is unavailable.
///
/// This layer sits above NetworkManager: SteamNetworking handles matchmaking
/// and relay, then hands off the actual peer connection to NetworkManager.
/// </summary>
public sealed class SteamNetworking : IDisposable
{
    private bool _initialized;

    /// <summary>True if Steam is running and networking is available.</summary>
    public bool IsAvailable => _initialized;

    /// <summary>The Steam lobby ID, or 0 if not in a lobby.</summary>
    public ulong CurrentLobbyId { get; private set; }

    /// <summary>
    /// Raised when a lobby is successfully created. Parameter is the lobby ID.
    /// </summary>
    public event Action<ulong>? OnLobbyCreated;

    /// <summary>
    /// Raised when successfully joined a lobby. Parameter is the lobby ID.
    /// </summary>
    public event Action<ulong>? OnLobbyJoined;

    /// <summary>
    /// Raised when a new member joins the current lobby. Parameter is their Steam ID.
    /// </summary>
    public event Action<ulong>? OnMemberJoined;

    /// <summary>
    /// Raised when a member leaves the current lobby. Parameter is their Steam ID.
    /// </summary>
    public event Action<ulong>? OnMemberLeft;

    /// <summary>
    /// Initialize Steam networking. Returns false if Steam is not running or
    /// Steamworks.NET is not linked.
    /// </summary>
    public bool Init()
    {
        // Steamworks.NET integration: once linked, this will call SteamAPI.Init()
        // and set up the P2P networking relay. For now, return false to signal
        // that the caller should use direct ENet connections.
        //
        // Integration steps (when Steamworks.NET NuGet is added):
        // 1. Call SteamAPI.Init() -- returns false if Steam client is not running
        // 2. Register lobby/P2P callbacks via SteamMatchmaking and SteamNetworking
        // 3. Set _initialized = true
        // 4. Start the SteamAPI.RunCallbacks() pump in Update()
        Console.WriteLine("[SteamNetworking] Steam integration not yet available. Using ENet fallback.");
        _initialized = false;
        return false;
    }

    /// <summary>
    /// Host a lobby for multiplayer. Returns the lobby ID (0 if failed).
    /// The lobby is created as a friends-only lobby. Call SetLobbyPublic()
    /// to make it visible in the server browser.
    /// </summary>
    public ulong CreateLobby(int maxPlayers = 4)
    {
        if (!_initialized)
        {
            Console.WriteLine("[SteamNetworking] Cannot create lobby: Steam not initialized.");
            return 0;
        }

        // When Steamworks.NET is integrated:
        // SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxPlayers);
        // The result arrives via OnLobbyCreated callback.
        return 0;
    }

    /// <summary>
    /// Join an existing lobby by Steam lobby ID.
    /// </summary>
    public bool JoinLobby(ulong lobbyId)
    {
        if (!_initialized)
        {
            Console.WriteLine("[SteamNetworking] Cannot join lobby: Steam not initialized.");
            return false;
        }

        // When Steamworks.NET is integrated:
        // SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        // The result arrives via OnLobbyJoined callback.
        return false;
    }

    /// <summary>
    /// Leave the current lobby.
    /// </summary>
    public void LeaveLobby()
    {
        if (!_initialized || CurrentLobbyId == 0) return;

        // When Steamworks.NET is integrated:
        // SteamMatchmaking.LeaveLobby(new CSteamID(CurrentLobbyId));
        CurrentLobbyId = 0;
    }

    /// <summary>
    /// Send data to a specific Steam user via P2P relay.
    /// Uses Steam's relay servers for NAT traversal.
    /// </summary>
    public bool SendTo(ulong steamId, byte[] data, bool reliable = true)
    {
        if (!_initialized) return false;

        // When Steamworks.NET is integrated:
        // var sendType = reliable
        //     ? EP2PSend.k_EP2PSendReliable
        //     : EP2PSend.k_EP2PSendUnreliableNoDelay;
        // return SteamNetworking.SendP2PPacket(new CSteamID(steamId), data, (uint)data.Length, sendType);
        return false;
    }

    /// <summary>
    /// Poll for Steam callbacks. Call once per frame alongside NetworkManager.PollEvents().
    /// </summary>
    public void Update()
    {
        if (!_initialized) return;

        // When Steamworks.NET is integrated:
        // SteamAPI.RunCallbacks();
    }

    /// <summary>
    /// Get the IP:port of the lobby host for direct ENet fallback connection.
    /// Returns null if the lobby metadata doesn't contain connection info.
    /// </summary>
    public (string host, ushort port)? GetLobbyHostAddress()
    {
        if (!_initialized || CurrentLobbyId == 0) return null;

        // When Steamworks.NET is integrated:
        // string? host = SteamMatchmaking.GetLobbyData(lobbyId, "host_ip");
        // string? portStr = SteamMatchmaking.GetLobbyData(lobbyId, "host_port");
        // if (host != null && ushort.TryParse(portStr, out ushort port))
        //     return (host, port);
        return null;
    }

    public void Dispose()
    {
        LeaveLobby();

        if (_initialized)
        {
            // When Steamworks.NET is integrated:
            // SteamAPI.Shutdown();
            _initialized = false;
        }
    }
}
