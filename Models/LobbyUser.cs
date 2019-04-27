namespace CrimsonDev.Gameteki.LobbyNode.Models
{
    using System.Collections.Generic;

    public class LobbyUser
    {
        public LobbyUser()
        {
            BlockList = new List<string>();
        }

        public string ConnectionId { get; set; }
        public string Name { get; set; }
        public string Node { get; set; }
        public List<string> BlockList { get; set; }
        public string UserData { get; set; }

        public bool HasUserBlocked(LobbyUser otherUser)
        {
            return BlockList.Contains(otherUser.Name);
        }
    }
}
