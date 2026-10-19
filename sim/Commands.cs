namespace Battler.Sim;

/// <summary>
/// Everything a player can do. Commands are the only input to the simulation, so in a networked
/// match they are all that travels between machines.
/// </summary>
public abstract record Command(int Player);

public sealed record PlaceFactory(int Player, FixVec2 Pos) : Command(Player);

/// <summary>Starts one of the player's tactical pauses.</summary>
public sealed record Pause(int Player) : Command(Player);

/// <summary>Ends a pause early; only the player who called it can.</summary>
public sealed record Resume(int Player) : Command(Player);
