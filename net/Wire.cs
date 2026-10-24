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
            case QueueUnit qu: w.Write((byte)19); w.Write((byte)qu.Player); w.Write((byte)qu.What); break;
            case Unqueue uq: w.Write((byte)20); w.Write((byte)uq.Player); w.Write((byte)uq.Index); break;
            case SetRepeat rp: w.Write((byte)21); w.Write((byte)rp.Player); w.Write(rp.On); break;
            case Research rs: w.Write((byte)22); w.Write((byte)rs.Player); w.Write((byte)rs.Tech); break;
            case CancelResearch cr: w.Write((byte)23); w.Write((byte)cr.Player); w.Write((byte)cr.Index); break;
            case Pause: w.Write((byte)2); w.Write((byte)c.Player); break;
            case Resume: w.Write((byte)3); w.Write((byte)c.Player); break;
            case UpgradeFactory uf: w.Write((byte)4); w.Write((byte)uf.Player); w.Write(uf.FactoryId); w.Write((byte)uf.Which); break;
            case SetRally sr: w.Write((byte)5); w.Write((byte)sr.Player); w.Write(sr.FactoryId); w.Write(sr.Clear); w.Write(sr.Pos.X.Raw); w.Write(sr.Pos.Z.Raw); break;
            case Sell se: w.Write((byte)7); w.Write((byte)se.Player); w.Write(se.BuildingId); break;
            case CancelUpgrades cu: w.Write((byte)12); w.Write((byte)cu.Player); w.Write(cu.BuildingId); break;
            case PlaceDefence pd: w.Write((byte)15); w.Write((byte)pd.Player); w.Write(pd.Pos.X.Raw); w.Write(pd.Pos.Z.Raw); w.Write((byte)pd.What); break;
            case MoveUnits mu:
                w.Write((byte)11); w.Write((byte)mu.Player); w.Write(mu.Pos.X.Raw); w.Write(mu.Pos.Z.Raw);
                w.Write((ushort)mu.Ids.Length); foreach (var id in mu.Ids) w.Write(id);
                break;
            default: throw new InvalidDataException($"no wire format for {c.GetType().Name}");
        }
    }

    static Command ReadCommand(BinaryReader r)
    {
        var type = r.ReadByte();
        int player = r.ReadByte();
        return type switch
        {
            19 => new QueueUnit(player, (Kind)r.ReadByte()),
            20 => new Unqueue(player, r.ReadByte()),
            21 => new SetRepeat(player, r.ReadBoolean()),
            22 => new Research(player, (Tech)r.ReadByte()),
            23 => new CancelResearch(player, r.ReadByte()),
            2 => new Pause(player),
            3 => new Resume(player),
            4 => new UpgradeFactory(player, r.ReadInt32(), (Upgrade)r.ReadByte()),
            5 => new SetRally(player, r.ReadInt32(), r.ReadBoolean(), new FixVec2(Fix.FromRaw(r.ReadInt64()), Fix.FromRaw(r.ReadInt64()))),
            7 => new Sell(player, r.ReadInt32()),
            11 => ReadMove(player, r),
            12 => new CancelUpgrades(player, r.ReadInt32()),
            15 => new PlaceDefence(player, new FixVec2(Fix.FromRaw(r.ReadInt64()), Fix.FromRaw(r.ReadInt64())), (Kind)r.ReadByte()),
            _ => throw new InvalidDataException($"unknown command type {type}"),
        };
    }

    static MoveUnits ReadMove(int player, BinaryReader r)
    {
        var pos = new FixVec2(Fix.FromRaw(r.ReadInt64()), Fix.FromRaw(r.ReadInt64()));
        var ids = new int[r.ReadUInt16()];
        for (int i = 0; i < ids.Length; i++) ids[i] = r.ReadInt32();
        return new MoveUnits(player, ids, pos);
    }
}
