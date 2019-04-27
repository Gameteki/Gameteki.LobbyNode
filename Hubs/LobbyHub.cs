namespace CrimsonDev.Gameteki.LobbyNode.Hubs
{
    using System;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Security.Claims;
    using System.Text;
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Data.Constants;
    using CrimsonDev.Gameteki.LobbyNode.Config;
    using CrimsonDev.Gameteki.LobbyNode.Models;
    using CrimsonDev.Gameteki.LobbyNode.Services;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Options;
    using Microsoft.IdentityModel.Tokens;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class LobbyHub : Hub<ILobbyClient>, ILobbyHub
    {
        private static bool setupDone;
        private readonly ILobbyService lobbyService;
        private readonly IGameNodeService gameNodeService;
        private readonly IDatabase database;
        private readonly AuthTokenOptions tokenOptions;

        public LobbyHub(ILobbyService lobbyService, IConnectionMultiplexer redisConnection, IGameNodeService gameNodeService, IOptions<AuthTokenOptions> tokenOptions)
        {
            this.lobbyService = lobbyService;
            this.gameNodeService = gameNodeService;

            this.tokenOptions = tokenOptions.Value;

            var subscriber = redisConnection.GetSubscriber();
            database = redisConnection.GetDatabase();

            if (setupDone)
            {
                return;
            }

            subscriber.Subscribe(RedisChannels.LobbyMessage).OnMessage(OnLobbyMessageAsync);
            subscriber.Subscribe(RedisChannels.LobbyMessageRemoved).OnMessage(OnLobbyMessageRemovedAsync);
            subscriber.Subscribe("RemoveRunningGame").OnMessage(OnRemoveGameAsync);

            setupDone = true;
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
                var blockList = encodedBlockList != null &&
                                !string.IsNullOrEmpty(encodedBlockList.Value) ? JsonConvert.DeserializeObject<List<string>>(encodedBlockList.Value) : new List<string>();
                var userData = Context.User.FindFirst("UserData");

                lobbyUser = new LobbyUser
                {
                    ConnectionId = Context.ConnectionId,
                    Name = Context.User.Identity.Name,
                    BlockList = blockList,
                    UserData = userData.Value ?? string.Empty
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
                result.Game.GetPlayers().Any(player => player.Value.User.HasUserBlocked(lobbyUser) || lobbyUser.HasUserBlocked(player.Value.User)));

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
                game.GetPlayers().Any(player => player.Value.User.HasUserBlocked(lobbyUser) || lobbyUser.HasUserBlocked(player.Value.User)));

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
                result.Game.GetPlayers().Any(player => player.Value.User.HasUserBlocked(lobbyUser) || lobbyUser.HasUserBlocked(player.Value.User)));

            await Clients.AllExcept(blockedUsers.Select(u => u.ConnectionId).ToList()).UpdateGame(result.Game.ToGameListSummary());
            await Groups.AddToGroupAsync(Context.ConnectionId, result.Game.Id.ToString());

            await SendGameStateAsync(result.Game);
        }

        [HubMethodName(LobbyMessages.StartGame)]
        [Authorize]
        public async Task StartGameAsync()
        {
            var result = await lobbyService.StartGameAsync(Context.ConnectionId);

            if (!result.Success)
            {
                await Clients.Caller.JoinFailed(result.Message);

                return;
            }

            var node = gameNodeService.GetNodeForGame();
            if (node == null)
            {
                await Clients.Caller.JoinFailed("Could not find a game node for your game.  Try again later.");

                return;
            }

            result.Game.Started = true;

            node.NumGames++;

            var blockedUsers = lobbyService.GetOnlineUsersForLobbyUser().Where(lobbyUser =>
                result.Game.GetPlayers().Any(player => player.Value.User.HasUserBlocked(lobbyUser) || lobbyUser.HasUserBlocked(player.Value.User)));

            await Clients.AllExcept(blockedUsers.Select(u => u.ConnectionId).ToList()).UpdateGame(result.Game.ToGameListSummary());

            await database.StringSetAsync($"game:{result.Game.Id.ToString()}", JsonConvert.SerializeObject(result.Game));
            await database.SetAddAsync("games", result.Game.Id.ToString());

            await SendGameStateAsync(result.Game);

            foreach (var playerOrSpectator in result.Game.GetPlayersAndSpectators())
            {
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(tokenOptions.Key));
                var jwt = new JwtSecurityToken(
                    tokenOptions.Issuer,
                    audience: tokenOptions.Issuer,
                    claims: new List<Claim> { new Claim(ClaimTypes.Name, playerOrSpectator.User.Name) },
                    notBefore: DateTime.UtcNow,
                    expires: DateTime.UtcNow.AddHours(2),
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

                var token = new JwtSecurityTokenHandler().WriteToken(jwt);

                await Clients.Client(playerOrSpectator.User.ConnectionId).HandOff(node.Address, node.Name, token, result.Game.Id);
            }
        }

        protected async Task SendGameStateAsync(LobbyGame game)
        {
            var gameSummary = game.ToGameSummary();

            await Clients.Group(gameSummary.Id).GameState(gameSummary);
        }

        private async Task OnRemoveGameAsync(ChannelMessage message)
        {
            var gameId = Guid.Parse(message.Message);

            lobbyService.RemoveGame(gameId);
            await Clients.All.RemoveGame(gameId);
        }

        private async Task OnLobbyMessageRemovedAsync(ChannelMessage message)
        {
            await Clients.All.RemoveLobbyMessage((int)message.Message);
        }

        private async Task OnLobbyMessageAsync(ChannelMessage channelMessage)
        {
            var message = JsonConvert.DeserializeObject<LobbyMessage>(channelMessage.Message);

            await Clients.All.LobbyChatMessage(message);
        }
    }
}
