namespace Gameteki.LobbyNode.Scheduler
{
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Data.Constants;
    using Gameteki.LobbyNode.Config;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Quartz;
    using StackExchange.Redis;

    public class NodeHeartbeat : IJob
    {
        private readonly ILogger<NodeHeartbeat> logger;
        private readonly GametekiLobbyOptions options;
        private readonly ISubscriber subscriber;

        public NodeHeartbeat(IConnectionMultiplexer redisConnection, ILogger<NodeHeartbeat> logger, IOptions<GametekiLobbyOptions> options)
        {
            this.logger = logger;
            this.options = options.Value;

            subscriber = redisConnection.GetSubscriber();
        }

        public async Task Execute(IJobExecutionContext context)
        {
            logger.LogDebug("Sending heartbeat for node", options.NodeName);
            await subscriber.PublishAsync(RedisChannels.LobbyHeartbeat, options.NodeName);
        }
    }
}
