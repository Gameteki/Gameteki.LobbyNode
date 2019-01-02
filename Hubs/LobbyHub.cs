namespace Gameteki.LobbyNode.Hubs
{
    using System;
    using System.Threading.Tasks;
    using Gameteki.LobbyNode.Services;
    using Microsoft.AspNetCore.SignalR;

    public class LobbyHub : Hub
    {
        private readonly ILobbyService lobbyService;

        public LobbyHub(ILobbyService lobbyService)
        {
            this.lobbyService = lobbyService;
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            if (Context.User.Identity.IsAuthenticated)
            {
                await lobbyService.DisconnectedUserAsync(Context.User.Identity.Name);
            }
        }

        public override async Task OnConnectedAsync()
        {
            if (Context.User.Identity.IsAuthenticated)
            {
                await lobbyService.NewUserAsync(Context.User.Identity.Name);
            }

            await Clients.Caller.SendAsync(Messages.UserList, lobbyService.GetUsers());
        }
    }
}
