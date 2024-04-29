using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;


public enum MessageType
{
    HandShake = -1,
    Console = 0,
    Position = 1,
    Ping = 2,
    Pong = 3,
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

        outData.AddRange(BitConverter.GetBytes(1));
        outData.AddRange(memoryStream.ToArray());

        return outData.ToArray();
    }
}

public class NetVector3 : IMessage<UnityEngine.Vector3>
{
    private static ulong lastMsgID = 0;
    private Vector3 data;

    public NetVector3(Vector3 data)
    {
        this.data = data;
    }

    public Vector3 Deserialize(byte[] message)
    {
        Vector3 outData;

        outData.x =
            outData.y = BitConverter.ToSingle(message, 12);
        outData.z = BitConverter.ToSingle(message, 16);

        return outData;
    }

    public MessageType GetMessageType()
    {
        return MessageType.Position;
    }

    public byte[] Serialize()
    {
        List<byte> outData = new List<byte>();

        outData.AddRange(BitConverter.GetBytes((int)GetMessageType()));
        outData.AddRange(BitConverter.GetBytes(lastMsgID++));
        outData.AddRange(BitConverter.GetBytes(data.x));
        outData.AddRange(BitConverter.GetBytes(data.y));
        outData.AddRange(BitConverter.GetBytes(data.z));

        return outData.ToArray();
    }

    //Dictionary<Client,Dictionary<msgType,int>>
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