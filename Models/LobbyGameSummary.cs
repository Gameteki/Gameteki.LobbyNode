namespace Gameteki.LobbyNode.Models
{
    using System.Collections.Generic;

    public class LobbyGameSummary : LobbyGameSummaryBase
    {
        public List<string> Spectators { get; set; }
        public Dictionary<string, LobbyPlayerSummary> Players { get; set; }
        public List<string> Messages { get; set; }
        public string Owner { get; set; }
    }
}
