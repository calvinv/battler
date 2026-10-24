namespace Battler.Sim;

/// <summary>
/// Everything a player can do. Commands are the only input to the simulation, so in a networked
/// match they are all that travels between machines.
/// </summary>
public abstract record Command(int Player);

/// <summary>Puts a unit on the end of the HQ's queue (paid for when a door starts it).</summary>
public sealed record QueueUnit(int Player, Kind What) : Command(Player);

/// <summary>Takes the unit at Index off the HQ's queue (nothing was paid for it yet).</summary>
public sealed record Unqueue(int Player, int Index) : Command(Player);

/// <summary>Whether finished fighting units go back on the end of the queue.</summary>
public sealed record SetRepeat(int Player, bool On) : Command(Player);

/// <summary>Puts research on the end of the research queue (paid for when it starts).</summary>
public sealed record Research(int Player, Tech Tech) : Command(Player);

/// <summary>Drops research Index from the queue, and anything after it that needs it (the one under way is refunded).</summary>
public sealed record CancelResearch(int Player, int Index) : Command(Player);

/// <summary>Starts one of the player's tactical pauses.</summary>
public sealed record Pause(int Player) : Command(Player);

/// <summary>Ends a pause early; only the player who called it can.</summary>
public sealed record Resume(int Player) : Command(Player);

/// <summary>Buys the next level of one of a defence's upgrades (queued if short of metal).</summary>
public sealed record UpgradeFactory(int Player, int FactoryId, Upgrade Which) : Command(Player);

/// <summary>Where the HQ's new units gather before attacking (Clear: none, so they attack).</summary>
public sealed record SetRally(int Player, int FactoryId, bool Clear, FixVec2 Pos) : Command(Player);

/// <summary>Sells one of the player's defences for World.SellBackPercent of all it cost, upgrades included.</summary>
public sealed record Sell(int Player, int BuildingId) : Command(Player);

/// <summary>Sends some of the player's scouts to Pos (they spread out round it).</summary>
public sealed record MoveUnits(int Player, int[] Ids, FixVec2 Pos) : Command(Player);

/// <summary>Drops all the upgrades queued at one of the player's buildings.</summary>
public sealed record CancelUpgrades(int Player, int BuildingId) : Command(Player);

/// <summary>A defence (World.DefenceKinds) at Pos, within reach of the player's HQ.</summary>
public sealed record PlaceDefence(int Player, FixVec2 Pos, Kind What) : Command(Player);
