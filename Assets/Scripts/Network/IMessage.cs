using System.Collections.Generic;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Game;
using Utils;


public enum MessageType
{
    HandShake = -1,
    Console,
    Position,
    Ping,
    Pong,
    Close,
    Dispose,
    Shoot
}


public interface IMessage<T>
{
    public MessageType GetMessageType();
    public byte[] Serialize();
    public T Deserialize(byte[] message);
}

public class NetClientToServerHS : IMessage<string>
{
    public string data;

    public string Deserialize(byte[] message)
    {
        string outData;

        outData = BitConverter.ToString(message, 4);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.HandShake;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

        foreach (char letter in data)
        {
            outData.Add((byte)letter);
        }

        return outData.ToArray();
    }
}

public class NetServerToClient : IMessage<Player[]>
{
    public Player[] data;

    public Player[] Deserialize(byte[] message)
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();

        byte[] playerArray = new byte[message.Length - 4];

        // Removes the message type from the array
        Array.Copy(message, 4, playerArray, 0, playerArray.Length);

        using MemoryStream memoryStream = new MemoryStream(playerArray);

        return (Player[])binaryFormatter.Deserialize(memoryStream);
    }

    public MessageType GetMessageType()
    {
        return MessageType.HandShake;
    }

    public byte[] Serialize()
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        using MemoryStream memoryStream = new MemoryStream();

        binaryFormatter.Serialize(memoryStream, data);

        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(memoryStream.ToArray());

        return outData.ToArray();
    }
}

public class NetVector3 : IMessage<(Vec3 pos, int id)>
{
    private static ulong lastMsgID = 0;
    public (Vec3 pos, int id) data;
    

    public NetVector3(byte[] data)
    {
        this.data = Deserialize(data);
    }

    public (Vec3 pos, int id) Deserialize(byte[] message)
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();

        byte[] newData = new byte[message.Length - 4];

        // Removes the message type from the array
        Array.Copy(message, 4, newData, 0, newData.Length);

        using MemoryStream memoryStream = new MemoryStream(newData);

        return ((Vec3 pos, int id))binaryFormatter.Deserialize(memoryStream);
    }

    public MessageType GetMessageType()
    {
        return MessageType.Position;
    }

    public byte[] Serialize()
    {
        BinaryFormatter binaryFormatter = new BinaryFormatter();
        using MemoryStream memoryStream = new MemoryStream();

        binaryFormatter.Serialize(memoryStream, data);

        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(memoryStream.ToArray());

        return outData.ToArray();
    }
}

public class NetConsole : IMessage<string>
{
    public string data;

    public MessageType messageType()
    {
        return MessageType.Console;
    }

    public string Deserialize(byte[] message)
    {
        byte[] messageWithoutHeader = new byte[message.Length - 4];

        Array.Copy(message, 4, messageWithoutHeader, 0, message.Length - 4);

        string outData = System.Text.Encoding.UTF8.GetString(messageWithoutHeader);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.HandShake;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));

        foreach (char letter in data)
        {
            outData.Add((byte)letter);
        }

        return outData.ToArray();
    }
}