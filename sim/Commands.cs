namespace Battler.Sim;

/// <summary>
/// Everything a player can do. Commands are the only input to the simulation, so in a networked
/// match they are all that travels between machines.
/// </summary>
public abstract record Command(int Player);

/// <summary>A factory at Pos that makes Makes (one of World.UnitKinds).</summary>
public sealed record PlaceFactory(int Player, FixVec2 Pos, Kind Makes) : Command(Player);

/// <summary>Starts one of the player's tactical pauses.</summary>
public sealed record Pause(int Player) : Command(Player);

/// <summary>Ends a pause early; only the player who called it can.</summary>
public sealed record Resume(int Player) : Command(Player);

/// <summary>Buys the next level of one of a factory's upgrades.</summary>
public sealed record UpgradeFactory(int Player, int FactoryId, Upgrade Which) : Command(Player);

/// <summary>Where a factory's units gather and hold (Clear: none, so they attack).</summary>
public sealed record SetRally(int Player, int FactoryId, bool Clear, FixVec2 Pos) : Command(Player);

/// <summary>Deploys an outpost (a sub-base: factories can be built near it) somewhere this side can see.</summary>
public sealed record PlaceOutpost(int Player, FixVec2 Pos) : Command(Player);
