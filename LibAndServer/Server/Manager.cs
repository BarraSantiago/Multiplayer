using System.Net;
using DLL;

namespace Server;

public class Manager : NetworkManager
{
    private bool countdownStarted;
    private DateTime countdownStartTime;

    public void Start(IPAddress ip, int port)
    {
        Port = port;
        IPAddress = ip;
        Connection = new UdpConnection(ip, port, this);
    }

    public void Update()
    {
        ReceiveData();
    }

    protected override void StartTimer()
    {
        Broadcast(BitConverter.GetBytes((int)MessageType.CountdownStarted));
    }

    protected override void GameWinner(byte[] data)
    {
        Broadcast(data);
    }

    protected override void HandleRejected(byte[] data)
    {
        throw new NotImplementedException();
    }

    protected override void ShootBullet(byte[] data)
    {
        Broadcast(data);
    }

    protected override void Close(IPEndPoint ip)
    {
        RemoveClient(ip);
        HandleHandshake(null, null);
    }

    protected override void SendPing()
    {
        throw new NotImplementedException();
    }

    protected override void ReceivePosition(byte[] data, IPEndPoint ip)
    {
        NetVec3 netVec3 = new NetVec3(data);
        _handshake.Players[_handshake.IPToId[ip]].pos = netVec3.Deserialize(data).pos;
    }
    
    protected override void ReceiveConsole(byte[] data)
    {
        NetConsole netConsole = new NetConsole();
        string message = netConsole.Deserialize(data);
        // TODO: Implement console message handling
    }

    protected override void ReceiveHandshake(byte[] data, IPEndPoint ip)
    {
        HandleHandshake(data, ip);
    }

    public void Broadcast(byte[] data)
    {
        using var iterator = Clients.GetEnumerator();

        while (iterator.MoveNext())
        {
            Connection.Send(data, iterator.Current.Value.ipEndPoint);
        }
    }

    private void HandleHandshake(byte[] data, IPEndPoint ip)
    {
        NetServerToClientHs netServerToClientHs = new NetServerToClientHs();

        Player player = new Player();
        player.clientID = _handshake.ServerReceiveHandshake(data, ip, Connection);

        if (player.clientID == -1) return;

        (int ID, string name, Vec3 pos)[] players = new (int ID, string name, Vec3 pos)[_handshake.Players.Count];

        int i = 0;
        foreach (var value in _handshake.Players)
        {
            players[i++] = (value.Key, value.Value.name, value.Value.pos);
        }

        netServerToClientHs.data = players;

        Broadcast(netServerToClientHs.Serialize());


        if (_handshake.Players.Count < 2 || countdownStarted) return;

        countdownStarted = true;
        countdownStartTime = DateTime.UtcNow;
        Broadcast(BitConverter.GetBytes((int)MessageType.CountdownStarted));
    }

    private void RemoveClient(IPEndPoint ip)
    {
        if (!_handshake.IPToId.TryGetValue(ip, out var id)) return;

        Connection.Send(BitConverter.GetBytes((int)MessageType.Close), ip);
        Clients.Remove(id);
        _handshake.Players.Remove(_handshake.IPToId[ip]);
        _handshake.IPToId.Remove(ip);
    }
}