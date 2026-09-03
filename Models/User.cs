// Models/User.cs
namespace GameRealmAPI.Models;

public enum AccountRole : byte
{
    Player    = 0,
    Moderator = 3,
    Admin     = 4
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;       // plain text
    public string? IP { get; set; }
    public bool? IsEmailVerified { get; set; } = false;
    public bool? IsActive { get; set; } = true;
    public bool? IsBanned { get; set; } = false;
    public AccountRole? Role { get; set; } = AccountRole.Player;
    public DateTime? CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
    public string? HWID { get; set; }

    // Navigation
    public Player? Player { get; set; }

    // Helper — returns the string name of the role for JWT claims etc.
    public string RoleName => Role?.ToString() ?? "Player"; // "Player", "Moderator", "Admin"
}
