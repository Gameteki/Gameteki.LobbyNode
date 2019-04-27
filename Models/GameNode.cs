namespace CrimsonDev.Gameteki.LobbyNode.Models
{
    using System;

    public class GameNode
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string Address { get; set; }
        public int NumGames { get; set; }
        public bool Enabled { get; set; }
        public bool Disconnected { get; set; }
        public DateTime LastHeartbeat { get; set; }
    }
}