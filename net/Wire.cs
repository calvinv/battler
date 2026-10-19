using System.Collections.Generic;
using System.IO;
using Battler.Sim;

namespace Battler.Net;

/// <summary>The messages two lockstep peers exchange, as bytes.</summary>
public static class Wire
{
    public const byte MsgStart = 1;   // host to client: seed; the match begins
    public const byte MsgInput = 2;   // a player's commands for one tick, plus a checksum

    public sealed class Input
    {
        public int Tick;
        public List<Command> Commands = new();
        public int SumTick;     // the sender's latest stepped tick...
        public ulong Sum;       // ...and its state checksum there
    }

    public static byte[] Start(ulong seed)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(MsgStart);
        w.Write(seed);
        return ms.ToArray();
    }

    public static byte[] EncodeInput(Input input)
    {
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write(MsgInput);
        w.Write(input.Tick);
        w.Write(input.SumTick);
        w.Write(input.Sum);
        w.Write((ushort)input.Commands.Count);
        foreach (var c in input.Commands) WriteCommand(w, c);
        return ms.ToArray();
    }

    public static byte Type(byte[] msg) => msg.Length > 0 ? msg[0] : (byte)0;

    public static ulong ReadStart(byte[] msg) => new BinaryReader(new MemoryStream(msg, 1, msg.Length - 1)).ReadUInt64();

    public static Input DecodeInput(byte[] msg)
    {
        var r = new BinaryReader(new MemoryStream(msg, 1, msg.Length - 1));
        var input = new Input { Tick = r.ReadInt32(), SumTick = r.ReadInt32(), Sum = r.ReadUInt64() };
        int n = r.ReadUInt16();
        for (int i = 0; i < n; i++) input.Commands.Add(ReadCommand(r));
        return input;
    }

    static void WriteCommand(BinaryWriter w, Command c)
    {
        switch (c)
        {
            case PlaceFactory pf: w.Write((byte)1); w.Write((byte)pf.Player); w.Write(pf.Pos.X.Raw); w.Write(pf.Pos.Z.Raw); break;
            case Pause: w.Write((byte)2); w.Write((byte)c.Player); break;
            case Resume: w.Write((byte)3); w.Write((byte)c.Player); break;
            default: throw new InvalidDataException($"no wire format for {c.GetType().Name}");
        }
    }

    static Command ReadCommand(BinaryReader r)
    {
        var type = r.ReadByte();
        int player = r.ReadByte();
        return type switch
        {
            1 => new PlaceFactory(player, new FixVec2(Fix.FromRaw(r.ReadInt64()), Fix.FromRaw(r.ReadInt64()))),
            2 => new Pause(player),
            3 => new Resume(player),
            _ => throw new InvalidDataException($"unknown command type {type}"),
        };
    }
}
