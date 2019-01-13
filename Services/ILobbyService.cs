namespace Gameteki.LobbyNode.Services
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Gameteki.LobbyNode.Models;

    public interface ILobbyService
    {
        void Init();
        Task NewUserAsync(LobbyUser user);
        Task<LobbyGame> DisconnectedUserAsync(string username);
        List<LobbyUser> GetOnlineUsersForLobbyUser(LobbyUser user = null);
        Task<GameResponse> StartNewGameAsync(string connectionId, StartNewGameRequest request);
        List<LobbyGameListSummary> GetGameListForLobbyUser(LobbyUser lobbyUser);
        LobbyGame FindGameForUser(string username);
        Task<LobbyGame> LeaveGameAsync(string connectionId);
        Task<GameResponse> JoinGameAsync(string connectionId, Guid gameId, string password);
    }
}
