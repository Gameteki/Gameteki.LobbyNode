namespace CrimsonDev.Gameteki.LobbyNode.Models
{
    using System;

    public class LobbyGameSummaryBase
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Started { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
