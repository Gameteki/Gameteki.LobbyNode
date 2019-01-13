namespace CrimsonDev.Gameteki.LobbyNode.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Data.Constants;
    using CrimsonDev.Gameteki.LobbyNode.Config;
    using CrimsonDev.Gameteki.LobbyNode.Models;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class LobbyService : ILobbyService
    {
        private readonly GametekiLobbyOptions options;
        private readonly ISubscriber subscriber;

        public LobbyService(IConnectionMultiplexer redisConnection, IOptions<GametekiLobbyOptions> options, ILogger<LobbyService> logger)
        {
            this.options = options.Value;
            Logger = logger;
            subscriber = redisConnection.GetSubscriber();

            UsersByConnectionId = new Dictionary<string, LobbyUser>();
            GamesById = new Dictionary<Guid, LobbyGame>();
        }

        protected Dictionary<string, LobbyUser> UsersByConnectionId { get; }
        protected Dictionary<Guid, LobbyGame> GamesById { get; }
        protected ILogger<LobbyService> Logger { get; }

        public void Init()
        {
            subscriber.Subscribe(RedisChannels.NewUser, OnNewUserMessage);
            subscriber.Subscribe(RedisChannels.UserDisconnect, OnUserDisconnectMessage);

            subscriber.Publish(RedisChannels.LobbyHello, options.NodeName);
        }

        public Task NewUserAsync(LobbyUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            return NewUserInternalAsync(user);
        }

        public Task<LobbyGame> DisconnectedUserAsync(string connectionId)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            return DisconnectedUserInternalAsync(connectionId);
        }

        public List<LobbyUser> GetOnlineUsersForLobbyUser(LobbyUser user = null)
        {
            if (user == null)
            {
                return UsersByConnectionId.Values.ToList();
            }

            return UsersByConnectionId.Values.Where(u => !user.BlockList.Contains(u.Name) && !u.BlockList.Contains(user.Name)).ToList();
        }

        public Task<GameResponse> StartNewGameAsync(string connectionId, StartNewGameRequest request)
        {
            if (connectionId == null)
            {
                throw new ArgumentNullException(nameof(connectionId));
            }

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return StartNewGameInternalAsync(connectionId, request);
        }

        public List<LobbyGameListSummary> GetGameListForLobbyUser(LobbyUser lobbyUser)
        {
            if (lobbyUser == null)
            {
                return GamesById.Values.Select(g => g.ToGameListSummary()).ToList();
            }

            return GamesById.Values
                .Where(g => !g.Players.Any(player => player.Value.User.HasUserBlocked(lobbyUser) && !lobbyUser.HasUserBlocked(player.Value.User)))
                .Select(g => g.ToGameListSummary()).ToList();
        }

        public LobbyGame FindGameForUser(string username)
        {
            return GamesById.Values.SingleOrDefault(g => g.HasPlayer(username));
        }

        public async Task<LobbyGame> LeaveGameAsync(string connectionId)
        {
            if (!UsersByConnectionId.ContainsKey(connectionId))
            {
                Logger.LogError($"Got leave game message for unknown connection id '{connectionId}'");
                return null;
            }

            var user = UsersByConnectionId[connectionId];
            var game = FindGameForUser(user.Name);
            if (game == null || game.Started)
            {
                return null;
            }

            game.PlayerLeave(user.Name);

            if (!game.IsEmpty())
            {
                await subscriber.PublishAsync(RedisChannels.UpdateGame, JsonConvert.SerializeObject(game));
                return game;
            }

            GamesById.Remove(game.Id);

            await subscriber.PublishAsync(RedisChannels.RemoveGame, game.Id.ToString());

            return game;
        }

        public async Task<GameResponse> JoinGameAsync(string connectionId, Guid gameId, string password)
        {
            if (!UsersByConnectionId.ContainsKey(connectionId))
            {
                Logger.LogError($"Got join game message for unknown connection id '{connectionId}'");
                return GameResponse.Failure("Connection not found");
            }

            var user = UsersByConnectionId[connectionId];
            if (!GamesById.ContainsKey(gameId))
            {
                Logger.LogError($"Got join game message for unknown game id '{gameId}'");
                return GameResponse.Failure("Game not found");
            }

            var game = GamesById[gameId];

            var joinResponse = game.Join(user, password);
            if (!joinResponse.Success)
            {
                return joinResponse;
            }

            await subscriber.PublishAsync(RedisChannels.UpdateGame, JsonConvert.SerializeObject(game));

            return joinResponse;
        }

        private async Task<GameResponse> StartNewGameInternalAsync(string connectionId, StartNewGameRequest request)
        {
            if (!UsersByConnectionId.ContainsKey(connectionId))
            {
                Logger.LogError($"Got new game message for unknown connection id '{connectionId}'");
                return GameResponse.Failure("Connection not found");
            }

            var user = UsersByConnectionId[connectionId];

            var existingGame = FindGameForUser(user.Name);
            if (existingGame != null)
            {
                Logger.LogError($"Got new game message for user already in game '{user.Name}' '{existingGame.Name}'");
                return GameResponse.Failure("You are already in a game so cannot start a new one");
            }

            if (request.QuickJoin)
            {
                var pendingGame = GamesById.Values.OrderBy(g => g.Started).FirstOrDefault(game => game.CanQuickJoin(game.GameType));
                if (pendingGame != null)
                {
                }

                return GameResponse.Succeeded(pendingGame);
            }

            var newGame = new LobbyGame(user.Name, request);

            newGame.NewGame(user);
            GamesById.Add(newGame.Id, newGame);

            await subscriber.PublishAsync(RedisChannels.NewGame, JsonConvert.SerializeObject(newGame));

            return GameResponse.Succeeded(newGame);
        }

        private async Task NewUserInternalAsync(LobbyUser user)
        {
            if (UsersByConnectionId.ContainsKey(user.ConnectionId))
            {
                Logger.LogError($"Got new user request for '{user.Name}' but already know this user");

                return;
            }

            user.Node = options.NodeName;

            UsersByConnectionId.Add(user.ConnectionId, user);

            await subscriber.PublishAsync(RedisChannels.NewUser, JsonConvert.SerializeObject(user));
        }

        private async Task<LobbyGame> DisconnectedUserInternalAsync(string connectionId)
        {
            if (!UsersByConnectionId.ContainsKey(connectionId))
            {
                Logger.LogError($"Got user disconnect for unknown connection Id '{connectionId}'");
                return null;
            }

            var user = UsersByConnectionId[connectionId];

            await subscriber.PublishAsync(RedisChannels.UserDisconnect, JsonConvert.SerializeObject(user));

            var game = FindGameForUser(user.Name);
            if (game == null || game.Started)
            {
                return null;
            }

            game.PlayerDisconnected(user.Name);

            if (!game.IsEmpty())
            {
                return game;
            }

            GamesById.Remove(game.Id);
            await subscriber.PublishAsync(RedisChannels.RemoveGame, game.Id.ToString());

            return game;
        }

        private void OnUserDisconnectMessage(RedisChannel channel, RedisValue user)
        {
            UsersByConnectionId.Remove(user);
        }

        private void OnNewUserMessage(RedisChannel channel, RedisValue value)
        {
            var lobbyUser = JsonConvert.DeserializeObject<LobbyUser>(value);

            if (lobbyUser.Node == options.NodeName)
            {
                return;
            }

            UsersByConnectionId.Add(lobbyUser.ConnectionId, lobbyUser);
        }
    }
}
