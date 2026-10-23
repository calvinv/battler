namespace Battler.Sim;

/// <summary>
/// Everything a player can do. Commands are the only input to the simulation, so in a networked
/// match they are all that travels between machines.
/// </summary>
public abstract record Command(int Player);

/// <summary>A wing built onto one of the player's bases, making Makes (one of their fighting units, or
/// Kind.MobileBase for a utility wing, which makes scouts and mobile bases to order).</summary>
public sealed record AddWing(int Player, int BaseId, Kind Makes) : Command(Player);

/// <summary>A wing snapped onto the end of line Slot of a host, a step on toward Toward (zero: straight on).</summary>
public sealed record AddWingAt(int Player, int BaseId, int Slot, Kind Makes, FixVec2 Toward = default) : Command(Player);

/// <summary>A module snapped onto the end of line Slot of a host: the wings after it on the line get what it does.</summary>
public sealed record AddModule(int Player, int BaseId, int Slot, Mod Mod, FixVec2 Toward = default) : Command(Player);

/// <summary>Starts one of the player's tactical pauses.</summary>
public sealed record Pause(int Player) : Command(Player);

/// <summary>Ends a pause early; only the player who called it can.</summary>
public sealed record Resume(int Player) : Command(Player);

/// <summary>Buys the next level of one of a factory's upgrades.</summary>
public sealed record UpgradeFactory(int Player, int FactoryId, Upgrade Which) : Command(Player);

/// <summary>Where a factory's units gather before attacking; for the HQ, the whole base's (Clear: none, so they attack).</summary>
public sealed record SetRally(int Player, int FactoryId, bool Clear, FixVec2 Pos) : Command(Player);

/// <summary>Sets up one of the player's mobile bases where it stands.</summary>
public sealed record Deploy(int Player, int UnitId) : Command(Player);

/// <summary>Packs up one of the player's set-up mobile bases (one with no wings) to move it.</summary>
public sealed record PackUp(int Player, int BaseId) : Command(Player);

/// <summary>Sells one of the player's factories or outposts for World.SellBackPercent of all it cost, upgrades included.</summary>
public sealed record Sell(int Player, int BuildingId) : Command(Player);

/// <summary>Builds an extractor on one of World.Deposits.</summary>
public sealed record PlaceExtractor(int Player, int Deposit) : Command(Player);

/// <summary>Pays for a scout at the player's first utility wing.</summary>
public sealed record QueueScout(int Player) : Command(Player);

/// <summary>Pays for a scout or a mobile base (What) at one of the player's utility wings.</summary>
public sealed record Order(int Player, int WingId, Kind What) : Command(Player);

/// <summary>Sends some of the player's scouts and mobile bases to Pos (they spread out round it).</summary>
public sealed record MoveUnits(int Player, int[] Ids, FixVec2 Pos) : Command(Player);

/// <summary>Drops all the upgrades queued at one of the player's buildings.</summary>
public sealed record CancelUpgrades(int Player, int BuildingId) : Command(Player);

/// <summary>What one of the player's factories fits its units with from now on (napalm needs sulfur).</summary>
public sealed record SetFitting(int Player, int FactoryId, Fitting Fitting) : Command(Player);

/// <summary>A defence (World.DefenceKinds) at Pos, within reach of one of the player's bases.</summary>
public sealed record PlaceDefence(int Player, FixVec2 Pos, Kind What) : Command(Player);

/// <summary>A power plant on one of World.Vents, within reach of one of the player's bases.</summary>
public sealed record PlacePlant(int Player, int Vent) : Command(Player);
