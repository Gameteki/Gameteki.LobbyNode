namespace Gameteki.LobbyNode.Hubs
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Gameteki.LobbyNode.Models;

    public interface ILobbyClient
    {
        Task UserList(List<string> users);
        Task GameList(List<LobbyGameListSummary> gameList);
        Task JoinFailed(string message);
        Task GameState(LobbyGameSummary gameSummary);
        Task NewGame(LobbyGameListSummary gameSummary);
        Task UpdateGame(LobbyGameListSummary gameSummary);
        Task RemoveGame(Guid id);
        Task LobbyChatMessage(LobbyMessage message);
        Task RemoveLobbyMessage(int messageId);
    }
}
