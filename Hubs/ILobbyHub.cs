namespace Gameteki.LobbyNode.Hubs
{
    using System;
    using System.Threading.Tasks;
    using Gameteki.LobbyNode.Models;

    public interface ILobbyHub
    {
        Task NewGameAsync(StartNewGameRequest request);
        Task LeaveGameAsync();
        Task JoinGameAsync(Guid gameId, string password = null);
    }
}
