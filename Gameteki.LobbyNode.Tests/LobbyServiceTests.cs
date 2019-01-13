namespace Gameteki.LobbyNode.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using CrimsonDev.Gameteki.Data.Constants;
    using Gameteki.LobbyNode.Config;
    using Gameteki.LobbyNode.Models;
    using Gameteki.LobbyNode.Services;
    using Microsoft.AspNetCore.SignalR;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json;
    using StackExchange.Redis;

    [TestClass]
    [ExcludeFromCodeCoverage]
    public class LobbyServiceTests
    {
        private Mock<IConnectionMultiplexer> RedisConnectionMock { get; set; }
        private Mock<ILogger<LobbyService>> LoggerMock { get; set; }
        private Mock<ISubscriber> SubscriberMock { get; set; }

        private IOptions<GametekiLobbyOptions> LobbyOptions { get; set; }
        private List<LobbyUser> TestUsers { get; set; }

        private LobbyService Service { get; set; }

        [TestInitialize]
        public void SetupTest()
        {
            RedisConnectionMock = new Mock<IConnectionMultiplexer>();
            LoggerMock = new Mock<ILogger<LobbyService>>();
            SubscriberMock = new Mock<ISubscriber>();

            LobbyOptions = new OptionsWrapper<GametekiLobbyOptions>(new GametekiLobbyOptions { NodeName = "TestNode" });
            TestUsers = new List<LobbyUser>();

            RedisConnectionMock.Setup(c => c.GetSubscriber(It.IsAny<object>())).Returns(SubscriberMock.Object);

            Service = new LobbyService(RedisConnectionMock.Object, LobbyOptions, LoggerMock.Object);

            for (var i = 0; i < 50; i++)
            {
                var testUser = TestUtils.GetRandomLobbyUser();

                TestUsers.Add(testUser);

                Service.NewUserAsync(testUser);
            }

            SubscriberMock.Reset();
        }

        [TestClass]
        public class Init : LobbyServiceTests
        {
            [TestMethod]
            public void WhenCalledBroadcastsHello()
            {
                Service.Init();

                SubscriberMock.Verify(s => s.Publish(It.Is<RedisChannel>(channel => channel == RedisChannels.LobbyHello), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
            }
        }

        [TestClass]
        public class NewUserAsync : LobbyServiceTests
        {
            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public async Task WhenUserIsNullThrowsException()
            {
                await Service.NewUserAsync(null);
            }

            [TestMethod]
            public async Task WhenCalledSetsNodeAndBroadcastsUser()
            {
                var user = new LobbyUser { Name = "TestUser", ConnectionId = Guid.NewGuid().ToString() };

                await Service.NewUserAsync(user);

                Assert.AreEqual(LobbyOptions.Value.NodeName, user.Node);
                SubscriberMock.Verify(s => s.PublishAsync(It.Is<RedisChannel>(channel => channel == RedisChannels.NewUser), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
            }

            [TestMethod]
            public async Task WhenCalledTwiceDoesNotBroadcastsUser()
            {
                var user = new LobbyUser { Name = "TestUser", ConnectionId = Guid.NewGuid().ToString() };

                await Service.NewUserAsync(user);
                await Service.NewUserAsync(user);

                SubscriberMock.Verify(
                    s => s.PublishAsync(It.Is<RedisChannel>(channel => channel == RedisChannels.NewUser), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.AtMostOnce);
            }
        }

        [TestClass]
        public class DisconnectedUserAsync : LobbyServiceTests
        {
            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public async Task WhenConnectionIdIsNullThrowsException()
            {
                await Service.DisconnectedUserAsync(null);
            }

            [TestMethod]
            public async Task WhenUserNotFoundDoesNotBroadcast()
            {
                await Service.DisconnectedUserAsync("TestId");

                SubscriberMock.Verify(
                    s => s.PublishAsync(It.Is<RedisChannel>(channel => channel == RedisChannels.UserDisconnect), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Never);
            }

            [TestMethod]
            public async Task WhenUserFoundBroadcastsUserDisconnect()
            {
                var user = new LobbyUser { Name = "TestUser", ConnectionId = Guid.NewGuid().ToString() };

                await Service.NewUserAsync(user);
                await Service.DisconnectedUserAsync(user.ConnectionId);

                var jsonUser = JsonConvert.SerializeObject(user);

                SubscriberMock.Verify(
                    s => s.PublishAsync(
                        It.Is<RedisChannel>(channel => channel == RedisChannels.UserDisconnect),
                        It.Is<RedisValue>(value => value == jsonUser),
                        It.IsAny<CommandFlags>()),
                    Times.Once);
            }
        }

        [TestClass]
        public class GetOnlineUsersForLobbyUser : LobbyServiceTests
        {
            [TestMethod]
            public void WhenUserIsNullReturnsAllUsers()
            {
                var result = Service.GetOnlineUsersForLobbyUser(null);

                Assert.AreEqual(TestUsers.Count, result.Count);
                CollectionAssert.AreEquivalent(TestUsers, result);
            }

            [TestMethod]
            public void WhenUserHasNoBlocklistReturnsAllUsers()
            {
                var result = Service.GetOnlineUsersForLobbyUser(TestUsers.First());

                Assert.AreEqual(TestUsers.Count, result.Count);
                CollectionAssert.AreEquivalent(TestUsers, result);
            }

            [TestMethod]
            public void WhenAnyUserHasSuppliedUserOnBlockListReturnsAllExceptThatUser()
            {
                var firstUser = TestUsers.First();
                var fourthUser = TestUsers.ElementAt(4);

                fourthUser.BlockList.Add(firstUser.Name);

                var result = Service.GetOnlineUsersForLobbyUser(firstUser);

                Assert.AreEqual(TestUsers.Count - 1, result.Count);
                CollectionAssert.DoesNotContain(result, fourthUser);
            }

            [TestMethod]
            public void WhenSuppliedUserHasUserOnTheirBlockListReturnsAllExceptThatUser()
            {
                var sixthUser = TestUsers.ElementAt(6);
                var firstUser = TestUsers.First();

                firstUser.BlockList.Add(sixthUser.Name);

                var result = Service.GetOnlineUsersForLobbyUser(firstUser);

                Assert.AreEqual(TestUsers.Count - 1, result.Count);
                CollectionAssert.DoesNotContain(result, sixthUser);
            }
        }

        [TestClass]
        public class StartNewGameAsync : LobbyServiceTests
        {
            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public async Task WhenNullConnectionIdThrowsException()
            {
                await Service.StartNewGameAsync(null, new StartNewGameRequest());
            }

            [TestMethod]
            [ExpectedException(typeof(ArgumentNullException))]
            public async Task WhenNullRequestThrowsException()
            {
                await Service.StartNewGameAsync("TestId", null);
            }

            [TestMethod]
            public async Task WhenConnectionIdNotFoundReturnsFailure()
            {
                var result = await Service.StartNewGameAsync("TestId", new StartNewGameRequest());

                Assert.IsFalse(result.Success);
            }

            [TestMethod]
            public async Task WhenUserIsAlreadyInGameReturnsFailure()
            {
                var firstUser = TestUsers.First();

                await Service.StartNewGameAsync(firstUser.ConnectionId, new StartNewGameRequest());
                var result = await Service.StartNewGameAsync(firstUser.ConnectionId, new StartNewGameRequest());

                Assert.IsFalse(result.Success);
            }

            [TestClass]
            public class WhenNewGameCreated : LobbyServiceTests
            {
                [TestMethod]
                public async Task ReturnsSuccessAndBroadcastsToAllUsers()
                {
                    var firstUser = TestUsers.First();

                    var result = await Service.StartNewGameAsync(firstUser.ConnectionId, new StartNewGameRequest());

                    Assert.IsTrue(result.Success);

                    SubscriberMock.Verify(s => s.PublishAsync(It.Is<RedisChannel>(channel => channel == RedisChannels.NewGame), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
                }

                /* TODO Move these tests to the hub
                [TestMethod]
                public async Task DoesNotSendToUsersWithCreatorOnBlockList()
                {
                    var sixthUser = TestUsers.ElementAt(6);
                    var firstUser = TestUsers.First();

                    sixthUser.BlockList.Add(firstUser.Name);

                    var result = await Service.StartNewGameAsync(firstUser.ConnectionId, new StartNewGameRequest());

                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(1, ExcludedClients.Count);
                    CollectionAssert.Contains(ExcludedClients, sixthUser.ConnectionId);

                    SubscriberMock.Verify(s => s.PublishAsync(It.Is<RedisChannel>(channel => channel == RedisChannels.NewGame), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
                    ClientProxyMock.Verify(c => c.SendCoreAsync(LobbyMessages.NewGame, It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
                }

                [TestMethod]
                public async Task DoesNotSendToUsersCreatorHasOnBlockList()
                {
                    var sixthUser = TestUsers.ElementAt(6);
                    var firstUser = TestUsers.First();

                    firstUser.BlockList.Add(sixthUser.Name);

                    var result = await Service.StartNewGameAsync(firstUser.ConnectionId, new StartNewGameRequest());

                    Assert.IsTrue(result.Success);
                    Assert.AreEqual(1, ExcludedClients.Count);
                    CollectionAssert.Contains(ExcludedClients, sixthUser.ConnectionId);

                    SubscriberMock.Verify(s => s.PublishAsync(It.Is<RedisChannel>(channel => channel == RedisChannels.NewGame), It.IsAny<RedisValue>(), It.IsAny<CommandFlags>()), Times.Once);
                    ClientProxyMock.Verify(c => c.SendCoreAsync(LobbyMessages.NewGame, It.IsAny<object[]>(), It.IsAny<CancellationToken>()), Times.Once);
                }*/
            }
        }
    }
}
