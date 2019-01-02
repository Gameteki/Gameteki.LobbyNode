namespace Gameteki.LobbyNode.Services
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Api.Models.Api;
    using CrimsonDev.Gameteki.Data.Constants;
    using Gameteki.LobbyNode.Config;
    using Gameteki.LobbyNode.Hubs;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class LobbyService : ILobbyService
    {
        private readonly GametekiLobbyOptions options;
        private readonly ISubscriber subscriber;
        private readonly IDatabase database;
        private readonly List<string> users;

        private readonly IHubContext<LobbyHub> hubContext;
        private ILogger<LobbyHub> logger;

        public LobbyService(IConnectionMultiplexer redisConnection, IOptions<GametekiLobbyOptions> options, IHubContext<LobbyHub> hubContext, ILogger<LobbyHub> logger)
        {
            this.hubContext = hubContext;
            this.logger = logger;
            this.options = options.Value;
            subscriber = redisConnection.GetSubscriber();
            database = redisConnection.GetDatabase();

            users = new List<string>(database.SetMembers(RedisKeys.Users).ToStringArray());
        }

        public void Init()
        {
            subscriber.Subscribe(RedisChannels.NewUser, OnNewUserMessage);
            subscriber.Subscribe(RedisChannels.UserDisconnect, OnUserDisconnectMessage);
            subscriber.Subscribe(RedisChannels.LobbyMessage, OnLobbyMessage);
            subscriber.Subscribe(RedisChannels.LobbyMessageRemoved, OnLobbyMessageRemoved);

            subscriber.Publish(RedisChannels.LobbyHello, options.NodeName);
        }

        public async Task NewUserAsync(string username)
        {
            await subscriber.PublishAsync(RedisChannels.NewUser, username);

            database.SetAdd(RedisKeys.Users, username);
        }

        public async Task DisconnectedUserAsync(string username)
        {
            await subscriber.PublishAsync(RedisChannels.UserDisconnect, username);

            database.SetRemove(RedisKeys.Users, username);
        }

        public List<string> GetUsers()
        {
            return users;
        }

        private void OnLobbyMessageRemoved(RedisChannel channel, RedisValue messageId)
        {
            hubContext.Clients.All.SendAsync("removemessage", (int)messageId);
        }

        private void OnLobbyMessage(RedisChannel channel, RedisValue messageString)
        {
            var message = JsonConvert.DeserializeObject<ApiLobbyMessage>(messageString);

            hubContext.Clients.All.SendAsync("lobbychat", message).GetAwaiter().GetResult();
        }

        private void OnUserDisconnectMessage(RedisChannel channel, RedisValue user)
        {
            users.Remove(user);
        }

        private void OnNewUserMessage(RedisChannel channel, RedisValue user)
        {
            users.Add(user);
        }
    }
}
