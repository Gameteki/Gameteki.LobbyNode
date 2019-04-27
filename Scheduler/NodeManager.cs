namespace CrimsonDev.Gameteki.LobbyNode.Scheduler
{
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Data.Constants;
    using CrimsonDev.Gameteki.LobbyNode.Config;
    using CrimsonDev.Gameteki.LobbyNode.Services;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Quartz;
    using StackExchange.Redis;

    public class NodeManager : IJob
    {
        private readonly ILogger<NodeManager> logger;
        private readonly IGameNodeService gameNodeService;
        private readonly GametekiLobbyOptions options;
        private readonly ISubscriber subscriber;

        public NodeManager(IConnectionMultiplexer redisConnection, ILogger<NodeManager> logger, IOptions<GametekiLobbyOptions> options, IGameNodeService gameNodeService)
        {
            this.logger = logger;
            this.gameNodeService = gameNodeService;
            this.options = options.Value;

            subscriber = redisConnection.GetSubscriber();
        }

        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogDebug("Sending heartbeat for node", options.NodeName);
            await subscriber.PublishAsync(RedisChannels.LobbyHeartbeat, options.NodeName);

            gameNodeService.CheckForTimeouts();
        }
    }
}
