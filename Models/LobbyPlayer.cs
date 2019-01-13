namespace CrimsonDev.Gameteki.LobbyNode.Models
{
    public class LobbyPlayer
    {
        public LobbyPlayer()
        {
            CustomData = string.Empty;
        }

        public LobbyUser User { get; set; }
        public bool IsSpectator { get; set; }

        public string CustomData { get; set; }
    }
}
