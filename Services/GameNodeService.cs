namespace CrimsonDev.Gameteki.LobbyNode.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using CrimsonDev.Gameteki.LobbyNode.Models;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    public class GameNodeService : IGameNodeService
    {
        private readonly ILogger<GameNodeService> logger;
        private readonly Dictionary<string, GameNode> gameNodes;

        public GameNodeService(IConnectionMultiplexer redisConnection, ILogger<GameNodeService> logger)
        {
            this.logger = logger;
            var subscriber = redisConnection.GetSubscriber();

            subscriber.Subscribe("NodeHello", OnNodeHello);
            subscriber.Subscribe("NodeHeartbeat", OnNodeHeartbeat);

            gameNodes = new Dictionary<string, GameNode>();
        }

        public GameNode GetNodeForGame()
        {
            return gameNodes.Values.OrderBy(n => n.NumGames).FirstOrDefault(n => n.Enabled && !n.Disconnected);
        }

        public void CheckForTimeouts()
        {
            foreach (var (nodeName, node) in gameNodes)
            {
                if ((DateTime.UtcNow - node.LastHeartbeat).TotalMinutes <= 3)
                {
                    continue;
                }

                logger.LogError($"No heartbeat received from node {nodeName} in 3 minutes, disabling it");

                node.Disconnected = true;
            }
        }

        private void OnNodeHeartbeat(RedisChannel channel, RedisValue heartbeatMessage)
        {
            var nodeHello = JsonConvert.DeserializeObject<GameNodeHello>(heartbeatMessage);
            GameNode node;

            if (!gameNodes.ContainsKey(nodeHello.Name))
            {
                logger.LogWarning($"Got heartbeat from node {nodeHello.Name} we knew nothing about.  Adding it as a new node");

                node = new GameNode
                {
                    Name = nodeHello.Name,
                };

                gameNodes.Add(nodeHello.Name, node);
            }
            else
            {
                node = gameNodes[nodeHello.Name];
            }

            node.Address = nodeHello.Address;
            node.Version = nodeHello.Version;
            node.Enabled = true;
            node.LastHeartbeat = DateTime.UtcNow;
        }

        private void OnNodeHello(RedisChannel channel, RedisValue helloMessage)
        {
            var nodeHello = JsonConvert.DeserializeObject<GameNodeHello>(helloMessage);
            GameNode node;

            if (gameNodes.ContainsKey(nodeHello.Name))
            {
                logger.LogWarning($"Got Node Hello from node {nodeHello.Name} we already knew about, assumed reconnected");
                node = gameNodes[nodeHello.Name];
                node.Disconnected = false;
            }
            else
            {
                node = new GameNode
                {
                    Name = nodeHello.Name
                };

                gameNodes.Add(nodeHello.Name, node);
            }

            node.Address = nodeHello.Address;
            node.Version = nodeHello.Version;
            node.Enabled = true;
            node.LastHeartbeat = DateTime.UtcNow;
        }
    }
}
