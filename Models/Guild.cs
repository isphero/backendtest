// Models/Guild.cs — normal int Id
namespace GameRealmAPI.Models;

public class Guild
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LeaderPlayerId { get; set; }
    public int Level { get; set; } = 1;
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Player Leader { get; set; } = null!;
    public ICollection<Player> Members { get; set; } = new List<Player>();
}
