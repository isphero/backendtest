// Services/GameService.cs
using Microsoft.EntityFrameworkCore;
using GameRealmAPI.Data;
using GameRealmAPI.DTOs;

namespace GameRealmAPI.Services;

public class GameService
{
    private readonly AppDbContext _db;
    public GameService(AppDbContext db) { _db = db; }

    // ── Rankings ─────────────────────────────────────────────────────

    // Called by RanksController → GET /api/ranks/players
    public async Task<PagedResponse<PlayerRankDto>> GetTopPlayersAsync(int page = 1, int pageSize = 100)
    {
        var total = await _db.Players.CountAsync();
        var items = await _db.Players
            .Include(p => p.Guild)
            .OrderByDescending(p => p.Kills)
            .ThenByDescending(p => p.Level)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PlayerRankDto(
                0, p.Name, p.Class,
                p.Guild != null ? p.Guild.Name : "—",
                p.Level, p.Kills, p.Deaths, p.IsOnline ?? false))
            .ToListAsync();

        var ranked = items.Select((p, i) => p with { Rank = (page - 1) * pageSize + i + 1 }).ToList();
        return new PagedResponse<PlayerRankDto>(ranked, total, page, pageSize);
    }

    // Called by RanksController → GET /api/ranks/guilds
    public async Task<PagedResponse<GuildRankDto>> GetTopGuildsAsync(int page = 1, int pageSize = 50)
    {
        var total = await _db.Guilds.CountAsync();
        var items = await _db.Guilds
            .Include(g => g.Leader)
            .OrderByDescending(g => g.Wins)
            .ThenByDescending(g => g.Level)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GuildRankDto(
                0, g.Name, g.Leader.Name,
                g.Members.Count, g.Level, g.Wins, g.Losses))
            .ToListAsync();

        var ranked = items.Select((g, i) => g with { Rank = (page - 1) * pageSize + i + 1 }).ToList();
        return new PagedResponse<GuildRankDto>(ranked, total, page, pageSize);
    }

    // Aliases kept for backward compatibility with old code
    public Task<PagedResponse<PlayerRankDto>> GetPlayerRankingsAsync(int page = 1, int pageSize = 50)
        => GetTopPlayersAsync(page, pageSize);

    public Task<PagedResponse<GuildRankDto>> GetGuildRankingsAsync(int page = 1, int pageSize = 50)
        => GetTopGuildsAsync(page, pageSize);

    // ── Stats (called by StatsController) ────────────────────────────

    public async Task<HomeStatsResponse> GetHomeStatsAsync()
    {
        bool serverOnline = false;
        int onlinePlayers = 0;
        int maxOnlinePlayers = 0;
        string serverUptime = "0 hrs";
        string guildWinner = "None";
        string guildLeaderName = "None";
        string epkWinner = "None";
        string epkWinnerGuild = "None";

        string iniPath = @"C:\Users\crazy\Desktop\NewServerFolder\Main\Database\Status.ini";
        try
        {
            if (System.IO.File.Exists(iniPath))
            {
                var lines = await System.IO.File.ReadAllLinesAsync(iniPath);
                bool inStatusSection = false;
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        var section = trimmed.Substring(1, trimmed.Length - 2).Trim();
                        inStatusSection = string.Equals(section, "Status", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    if (inStatusSection)
                    {
                        var parts = trimmed.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var val = parts[1].Trim();

                            if (string.Equals(key, "ServerStatus", StringComparison.OrdinalIgnoreCase))
                            {
                                serverOnline = string.Equals(val, "Online", StringComparison.OrdinalIgnoreCase);
                            }
                            else if (string.Equals(key, "Online", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(val, out onlinePlayers);
                            }
                            else if (string.Equals(key, "MaxOnline", StringComparison.OrdinalIgnoreCase))
                            {
                                int.TryParse(val, out maxOnlinePlayers);
                            }
                            else if (string.Equals(key, "UpTime", StringComparison.OrdinalIgnoreCase))
                            {
                                serverUptime = val;
                            }
                            else if (string.Equals(key, "GuildWinner", StringComparison.OrdinalIgnoreCase))
                            {
                                guildWinner = val;
                            }
                            else if (string.Equals(key, "GuildLeaderName", StringComparison.OrdinalIgnoreCase))
                            {
                                guildLeaderName = val;
                            }
                            else if (string.Equals(key, "EpkChampion", StringComparison.OrdinalIgnoreCase))
                            {
                                epkWinner = val;
                            }
                            else if (string.Equals(key, "EpkChampionGuild", StringComparison.OrdinalIgnoreCase))
                            {
                                epkWinnerGuild = val;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading Status.ini: {ex.Message}");
        }

        var total = await _db.Players.CountAsync();
        var guilds = await _db.Guilds.CountAsync();

        return new HomeStatsResponse(
            ServerOnline: serverOnline,
            OnlinePlayers: onlinePlayers,
            MaxOnlinePlayers: maxOnlinePlayers,
            ServerUptime: serverUptime,
            GuildWinner: guildWinner,
            GuildLeaderName: guildLeaderName,
            EpkWinner: epkWinner,
            EpkWinnerGuild: epkWinnerGuild,
            TotalPlayers: total,
            TotalGuilds: guilds,
            Version: "v2.1.4"
        );
    }

    public async Task<ServerStatsResponse> GetServerStatsAsync()
    {
        var online = await AppDbContext.GetOnlinePlayerCount(_db, 0);
        var total = await _db.Players.CountAsync();
        var guilds = await _db.Guilds.CountAsync();
        var totalKills = await _db.Players.SumAsync(p => (long)p.Kills);

        return new ServerStatsResponse(
            OnlinePlayers: online,
            TotalPlayers: total,
            TotalGuilds: guilds,
            TotalKills: totalKills,
            ServerUptime: "99.9%",
            Version: "v2.1.4",
            LastUpdated: DateTime.UtcNow
        );
    }

    public async Task<int> GetOnlineCountAsync()
        => await AppDbContext.GetOnlinePlayerCount(_db, 0);
}