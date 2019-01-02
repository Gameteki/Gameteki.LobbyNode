namespace Gameteki.LobbyNode.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface ILobbyService
    {
        void Init();
        Task NewUserAsync(string username);
        Task DisconnectedUserAsync(string username);
        List<string> GetUsers();
    }
}
