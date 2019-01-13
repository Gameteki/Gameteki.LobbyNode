namespace CrimsonDev.Gameteki.LobbyNode.Services
{
    using StackExchange.Redis;

    public class RedisService : IRedisService
    {
        private readonly IConnectionMultiplexer redisConnection;

        public RedisService(IConnectionMultiplexer redisConnection)
        {
            this.redisConnection = redisConnection;
        }
    }
}
