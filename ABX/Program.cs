using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;


class Program
{
    const int Port = 3000;
    const string ServerIp = "127.0.0.1"; // You can change if need or with on deployment server

    static async Task Main(string[] args)
    {
        var packets = await StreamAllPackets(); // 
        var missingSequences = FindMissingSequences(packets);

        foreach (var missingSeq in missingSequences)
        {
            var packet = await RequestResendPacket(missingSeq);
            if (packet != null)
                packets.Add(packet);
        }

        packets.Sort((a, b) => a.PacketSequence.CompareTo(b.PacketSequence));
        await JsonToFile(packets);

        Console.WriteLine("Json File Saved Successfully! on 'D:\\PublishABX\\Result_Json\\' path");
        Console.ReadKey();
        Console.ReadKey();

    }

    static async Task<List<Packet>> StreamAllPackets() 
    {
        var packets = new List<Packet>();

        using var client = new TcpClient();
        await client.ConnectAsync(ServerIp, Port);

        using var stream = client.GetStream();

        
        var request = new byte[2] { 1, 0 }; 
        await stream.WriteAsync(request, 0, request.Length);

        var buffer = new byte[17]; 

        try
        {
            while (true)
            {
                int read = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0) break; 

                if (read == 17)
                {
                    var packet = ParsePacket(buffer);
                    packets.Add(packet);
                }
            }
        }
        catch (IOException)
        {
            
        }

        return packets;
    }

    static List<int> FindMissingSequences(List<Packet> packets)
    {
        var missing = new List<int>();

        var sequences = new HashSet<int>(packets.ConvertAll(p => p.PacketSequence));
        int minSeq = int.MaxValue;
        int maxSeq = int.MinValue;

        foreach (var p in packets)
        {
            if (p.PacketSequence < minSeq) minSeq = p.PacketSequence;
            if (p.PacketSequence > maxSeq) maxSeq = p.PacketSequence;
        }

        for (int i = minSeq; i <= maxSeq; i++)
        {
            if (!sequences.Contains(i))
                missing.Add(i);
        }

        return missing;
    }

    static async Task<Packet> RequestResendPacket(int sequenceNumber)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(ServerIp, Port);

        using var stream = client.GetStream();

        
        var request = new byte[2];
        request[0] = 2; 
        request[1] = (byte)sequenceNumber;

        await stream.WriteAsync(request, 0, request.Length);

        var buffer = new byte[17];
        int read = await stream.ReadAsync(buffer, 0, buffer.Length);

        if (read == 17)
            return ParsePacket(buffer);

        return null;
    }

    static Packet ParsePacket(byte[] buffer)
    {
        string symbol = Encoding.ASCII.GetString(buffer, 0, 4);
        char side = (char)buffer[4];
        int quantity = BitConverter.ToInt32(buffer[5..9].Reverse().ToArray(), 0);
        int price = BitConverter.ToInt32(buffer[9..13].Reverse().ToArray(), 0);
        int sequence = BitConverter.ToInt32(buffer[13..17].Reverse().ToArray(), 0);

        return new Packet
        {
            Symbol = symbol.Trim(),
            Side = side,
            Quantity = quantity,
            Price = price,
            PacketSequence = sequence
        };
    }

    static async Task JsonToFile(List<Packet> packets)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        string jsonString = JsonSerializer.Serialize(packets, options);

        await File.WriteAllTextAsync(@"D:\PublishABX\Result_Json\Result_" + DateTime.Now.ToString("dd.MM.yyyy")+".json", jsonString);
    }
}

class Packet
{
    public string Symbol { get; set; }
    public char Side { get; set; }
    public int Quantity { get; set; }
    public int Price { get; set; }
    public int PacketSequence { get; set; }
}
