namespace Gameteki.LobbyNode.Models
{
    public class GameResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public LobbyGame Game { get; set; }

        public static GameResponse Failure(string message)
        {
            return new GameResponse { Success = false, Message = message };
        }

        public static GameResponse Succeeded(LobbyGame game)
        {
            return new GameResponse { Success = true, Game = game };
        }
    }
}
