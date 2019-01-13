namespace Gameteki.LobbyNode.Models
{
    using System.Collections.Generic;

    public class LobbyGameListSummary : LobbyGameSummaryBase
    {
        public string Node { get; set; }
        public bool ShowHand { get; set; }
        public bool NeedsPassword { get; set; }
        public string GameType { get; set; }
        public string CustomData { get; set; }
        public List<string> Players { get; set; }
    }
}