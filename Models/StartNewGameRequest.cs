namespace CrimsonDev.Gameteki.LobbyNode.Models
{
    public class StartNewGameRequest
    {
        public string Name { get; set; }
        public bool Spectators { get; set; }
        public bool ShowHand { get; set; }
        public string Password { get; set; }
        public bool QuickJoin { get; set; }
        public string GameType { get; set; }
        public string CustomData { get; set; }
    }
}
