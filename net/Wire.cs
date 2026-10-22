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
            case AddWing aw: w.Write((byte)1); w.Write((byte)aw.Player); w.Write(aw.BaseId); w.Write((byte)aw.Makes); break;
            case Pause: w.Write((byte)2); w.Write((byte)c.Player); break;
            case Resume: w.Write((byte)3); w.Write((byte)c.Player); break;
            case UpgradeFactory uf: w.Write((byte)4); w.Write((byte)uf.Player); w.Write(uf.FactoryId); w.Write((byte)uf.Which); break;
            case SetRally sr: w.Write((byte)5); w.Write((byte)sr.Player); w.Write(sr.FactoryId); w.Write(sr.Clear); w.Write(sr.Pos.X.Raw); w.Write(sr.Pos.Z.Raw); break;
            case Deploy dp: w.Write((byte)6); w.Write((byte)dp.Player); w.Write(dp.UnitId); break;
            case Sell se: w.Write((byte)7); w.Write((byte)se.Player); w.Write(se.BuildingId); break;
            case PlaceExtractor pe: w.Write((byte)8); w.Write((byte)pe.Player); w.Write(pe.Deposit); break;
            case QueueScout: w.Write((byte)9); w.Write((byte)c.Player); break;
            case PackUp pu: w.Write((byte)10); w.Write((byte)pu.Player); w.Write(pu.BaseId); break;
            case CancelUpgrades cu: w.Write((byte)12); w.Write((byte)cu.Player); w.Write(cu.BuildingId); break;
            case Order od: w.Write((byte)13); w.Write((byte)od.Player); w.Write(od.WingId); w.Write((byte)od.What); break;
            case PlaceDefence pd: w.Write((byte)15); w.Write((byte)pd.Player); w.Write(pd.Pos.X.Raw); w.Write(pd.Pos.Z.Raw); w.Write((byte)pd.What); break;
            case PlacePlant pp: w.Write((byte)16); w.Write((byte)pp.Player); w.Write(pp.Vent); break;
            case SetFitting sf: w.Write((byte)14); w.Write((byte)sf.Player); w.Write(sf.FactoryId); w.Write((byte)sf.Fitting); break;
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
            1 => new AddWing(player, r.ReadInt32(), (Kind)r.ReadByte()),
            2 => new Pause(player),
            3 => new Resume(player),
            4 => new UpgradeFactory(player, r.ReadInt32(), (Upgrade)r.ReadByte()),
            5 => new SetRally(player, r.ReadInt32(), r.ReadBoolean(), new FixVec2(Fix.FromRaw(r.ReadInt64()), Fix.FromRaw(r.ReadInt64()))),
            6 => new Deploy(player, r.ReadInt32()),
            7 => new Sell(player, r.ReadInt32()),
            8 => new PlaceExtractor(player, r.ReadInt32()),
            9 => new QueueScout(player),
            10 => new PackUp(player, r.ReadInt32()),
            11 => ReadMove(player, r),
            12 => new CancelUpgrades(player, r.ReadInt32()),
            13 => new Order(player, r.ReadInt32(), (Kind)r.ReadByte()),
            15 => new PlaceDefence(player, new FixVec2(Fix.FromRaw(r.ReadInt64()), Fix.FromRaw(r.ReadInt64())), (Kind)r.ReadByte()),
            16 => new PlacePlant(player, r.ReadInt32()),
            14 => new SetFitting(player, r.ReadInt32(), (Fitting)r.ReadByte()),
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
