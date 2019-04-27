namespace CrimsonDev.Gameteki.LobbyNode.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.LobbyNode.Models;
    using StackExchange.Redis;

    public interface ILobbyService
    {
        void Init();
        Task NewUserAsync(LobbyUser user);
        Task<LobbyGame> DisconnectedUserAsync(string connectionId);
        List<LobbyUser> GetOnlineUsersForLobbyUser(LobbyUser user = null);
        Task<GameResponse> StartNewGameAsync(string connectionId, StartNewGameRequest request);
        List<LobbyGameListSummary> GetGameListForLobbyUser(LobbyUser lobbyUser);
        LobbyGame FindGameForUser(string username);
        Task<LobbyGame> LeaveGameAsync(string connectionId);
        Task<GameResponse> JoinGameAsync(string connectionId, Guid gameId, string password);
        Task<GameResponse> StartGameAsync(string connectionId);
        void RemoveGame(Guid gameId);
    }
}
