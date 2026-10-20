namespace Battler.Sim;

/// <summary>
/// Everything a player can do. Commands are the only input to the simulation, so in a networked
/// match they are all that travels between machines.
/// </summary>
public abstract record Command(int Player);

/// <summary>A factory at Pos that makes Makes (one of World.FactoryKinds).</summary>
public sealed record PlaceFactory(int Player, FixVec2 Pos, Kind Makes) : Command(Player);

/// <summary>Starts one of the player's tactical pauses.</summary>
public sealed record Pause(int Player) : Command(Player);

/// <summary>Ends a pause early; only the player who called it can.</summary>
public sealed record Resume(int Player) : Command(Player);

/// <summary>Buys the next level of one of a factory's upgrades.</summary>
public sealed record UpgradeFactory(int Player, int FactoryId, Upgrade Which) : Command(Player);

/// <summary>Where a factory's units gather before attacking; for the HQ, the whole base's (Clear: none, so they attack).</summary>
public sealed record SetRally(int Player, int FactoryId, bool Clear, FixVec2 Pos) : Command(Player);

/// <summary>Builds an outpost (a sub-base: factories can be built near it) on the pad beside deposit Deposit, which the player mines.</summary>
public sealed record PlaceOutpost(int Player, int Deposit) : Command(Player);

/// <summary>Sells one of the player's factories or outposts for World.SellBackPercent of all it cost, upgrades included.</summary>
public sealed record Sell(int Player, int BuildingId) : Command(Player);

/// <summary>Builds an extractor on one of World.Deposits.</summary>
public sealed record PlaceExtractor(int Player, int Deposit) : Command(Player);

/// <summary>Pays for a scout; the HQ builds it.</summary>
public sealed record QueueScout(int Player) : Command(Player);

/// <summary>Whether the HQ keeps building scouts by itself.</summary>
public sealed record SetAutoScout(int Player, bool On) : Command(Player);

/// <summary>Sends some of the player's scouts to Pos (they spread out round it).</summary>
public sealed record MoveUnits(int Player, int[] Ids, FixVec2 Pos) : Command(Player);

/// <summary>Drops all the upgrades queued at one of the player's buildings.</summary>
public sealed record CancelUpgrades(int Player, int BuildingId) : Command(Player);
