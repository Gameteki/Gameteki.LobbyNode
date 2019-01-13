namespace CrimsonDev.Gameteki.LobbyNode.Models
{
    using System.Collections.Generic;

    public class ClientHelloMessage
    {
        public List<string> Users { get; set; }
        public string Version { get; set; }
    }
}
