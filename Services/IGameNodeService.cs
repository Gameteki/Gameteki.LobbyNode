namespace CrimsonDev.Gameteki.LobbyNode.Services
{
    using CrimsonDev.Gameteki.LobbyNode.Models;

    public interface IGameNodeService
    {
        GameNode GetNodeForGame();
        void CheckForTimeouts();
    }
}
