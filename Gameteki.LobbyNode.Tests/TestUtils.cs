namespace Gameteki.LobbyNode.Tests
{
    using System;
    using Bogus;
    using Gameteki.LobbyNode.Models;

    public static class TestUtils
    {
        public static LobbyUser GetRandomLobbyUser()
        {
            var faker = new Faker();

            return new LobbyUser
            {
                Name = faker.Person.UserName,
                ConnectionId = Guid.NewGuid().ToString()
            };
        }
    }
}
