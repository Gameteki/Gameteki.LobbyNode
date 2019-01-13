namespace CrimsonDev.Gameteki.LobbyNode.Hubs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Data.Constants;
    using CrimsonDev.Gameteki.LobbyNode.Models;
    using CrimsonDev.Gameteki.LobbyNode.Services;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class LobbyHub : Hub<ILobbyClient>, ILobbyHub
    {
        private readonly ILobbyService lobbyService;

        public LobbyHub(ILobbyService lobbyService, IConnectionMultiplexer redisConnection)
        {
            this.lobbyService = lobbyService;

            var subscriber = redisConnection.GetSubscriber();

            subscriber.Subscribe(RedisChannels.LobbyMessage, OnLobbyMessage);
            subscriber.Subscribe(RedisChannels.LobbyMessageRemoved, OnLobbyMessageRemoved);
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var game = await lobbyService.DisconnectedUserAsync(Context.ConnectionId);

            if (game == null)
            {
                return;
            }

            if (game.IsEmpty())
            {
                await Clients.All.RemoveGame(game.Id);
            }

            await SendGameStateAsync(game);
        }

        public override async Task OnConnectedAsync()
        {
            LobbyUser lobbyUser = null;

            if (Context.User.Identity.IsAuthenticated)
            {
                var encodedBlockList = Context.User.FindFirst("BlockList");
                var blockList = encodedBlockList != null && !string.IsNullOrEmpty(encodedBlockList.Value) ? JsonConvert.DeserializeObject<List<string>>(encodedBlockList.Value) : new List<string>();

                lobbyUser = new LobbyUser
                {
                    ConnectionId = Context.ConnectionId,
                    Name = Context.User.Identity.Name,
                    BlockList = blockList
                };

                await lobbyService.NewUserAsync(lobbyUser);
            }

            await Clients.Caller.UserList(lobbyService.GetOnlineUsersForLobbyUser(lobbyUser).Select(u => u.Name).ToList());
            await Clients.Caller.GameList(lobbyService.GetGameListForLobbyUser(lobbyUser));
        }

        [HubMethodName(LobbyMessages.NewGame)]
        [Authorize]
        public async Task NewGameAsync(StartNewGameRequest request)
        {
            var result = await lobbyService.StartNewGameAsync(Context.ConnectionId, request);

            if (!result.Success)
            {
                await Clients.Caller.JoinFailed(result.Message);

                return;
            }

            var blockedUsers = lobbyService.GetOnlineUsersForLobbyUser().Where(lobbyUser =>
                result.Game.Players.Any(player => player.Value.User.HasUserBlocked(lobbyUser) || lobbyUser.HasUserBlocked(player.Value.User)));

            await Clients.AllExcept(blockedUsers.Select(u => u.ConnectionId).ToList()).NewGame(result.Game.ToGameListSummary());
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Game.Id.ToString());
            await SendGameStateAsync(result.Game);
        }

        [HubMethodName(LobbyMessages.LeaveGame)]
        [Authorize]
        public async Task LeaveGameAsync()
        {
            var game = await lobbyService.LeaveGameAsync(Context.ConnectionId);
            if (game == null)
            {
                return;
            }

            if (game.IsEmpty())
            {
                await Clients.All.RemoveGame(game.Id);

                return;
            }

            var blockedUsers = lobbyService.GetOnlineUsersForLobbyUser().Where(lobbyUser =>
                game.Players.Any(player => player.Value.User.HasUserBlocked(lobbyUser) || lobbyUser.HasUserBlocked(player.Value.User)));

            await Clients.AllExcept(blockedUsers.Select(u => u.ConnectionId).ToList()).UpdateGame(game.ToGameListSummary());

            await SendGameStateAsync(game);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, game.Id.ToString());
        }

        [HubMethodName(LobbyMessages.JoinGame)]
        [Authorize]
        public async Task JoinGameAsync(Guid gameId, string password = null)
        {
            var result = await lobbyService.JoinGameAsync(Context.ConnectionId, gameId, password);

            if (!result.Success)
            {
                await Clients.Caller.JoinFailed(result.Message);

                return;
            }

            var blockedUsers = lobbyService.GetOnlineUsersForLobbyUser().Where(lobbyUser =>
                result.Game.Players.Any(player => player.Value.User.HasUserBlocked(lobbyUser) || lobbyUser.HasUserBlocked(player.Value.User)));

            await Clients.AllExcept(blockedUsers.Select(u => u.ConnectionId).ToList()).UpdateGame(result.Game.ToGameListSummary());
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Game.Id.ToString());

            await SendGameStateAsync(result.Game);
        }

        [HubMethodName(LobbyMessages.StartGame)]
        public Task StartGameAsync()
        {
            return Task.CompletedTask;
        }

        protected async Task SendGameStateAsync(LobbyGame game)
        {
            var gameSummary = game.ToGameSummary();

            await Clients.Group(gameSummary.Id).GameState(gameSummary);
        }

        private void OnLobbyMessageRemoved(RedisChannel channel, RedisValue messageId)
        {
            Clients.All.RemoveLobbyMessage((int)messageId);
        }

        private void OnLobbyMessage(RedisChannel channel, RedisValue messageString)
        {
            var message = JsonConvert.DeserializeObject<LobbyMessage>(messageString);

            Clients.All.LobbyChatMessage(message).GetAwaiter().GetResult();
        }
    }
}
