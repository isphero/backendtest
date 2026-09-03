// DTOs/AuthDTOs.cs — updated to use Id not EntityID
namespace GameRealmAPI.DTOs;

public record LoginRequest(string Username, string Password);
public record RegisterRequest(string Username, string Email, string Password);
public record ForgotPasswordRequest(string Email);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record ResetPasswordRequest(string Token, string NewPassword);

public record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public record UserDto(int Id, string Username, string Email, string Role, DateTime? CreatedAt);

public record PlayerRankDto(int Rank, string Name, string Class, string GuildName,
    int Level, int Kills, int Deaths, bool IsOnline);

public record GuildRankDto(int Rank, string Name, string LeaderName,
    int MemberCount, int Level, int Wins, int Losses);

public record HomeStatsResponse(
    bool ServerOnline,
    int OnlinePlayers,
    int MaxOnlinePlayers,
    string ServerUptime,
    string GuildWinner,
    string GuildLeaderName,
    string EpkWinner,
    string EpkWinnerGuild,
    int TotalPlayers,
    int TotalGuilds,
    string Version);

public record ServerStatsResponse(int OnlinePlayers, int TotalPlayers,
    int TotalGuilds, long TotalKills, string ServerUptime, string Version, DateTime LastUpdated);

public record ApiResponse<T>(bool Success, string Message, T? Data = default);
public record ApiResponse(bool Success, string Message);
public record PagedResponse<T>(List<T> Items, int TotalCount, int Page, int PageSize);
