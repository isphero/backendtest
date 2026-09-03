// Models/Player.cs — normal int Id
namespace GameRealmAPI.Models;

public class Player
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Class { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public long Experience { get; set; } = 0;
    public int Kills { get; set; } = 0;
    public int Deaths { get; set; } = 0;
    public int? GuildId { get; set; }
    public bool? IsOnline { get; set; } = false;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; } = DateTime.UtcNow;
    public int Face { get; set; } = 1;

    // Navigation
    public User User { get; set; } = null!;
    public Guild? Guild { get; set; }
}
