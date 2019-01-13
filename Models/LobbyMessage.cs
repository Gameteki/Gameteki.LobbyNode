namespace Gameteki.LobbyNode.Models
{
    using System;

    public class LobbyMessage
    {
        public int Id { get; set; }
        public string User { get; set; }
        public string Message { get; set; }
        public DateTime Time { get; set; }
    }
}