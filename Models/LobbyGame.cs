namespace CrimsonDev.Gameteki.LobbyNode.Models
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class LobbyGame
    {
        private readonly Dictionary<string, LobbyPlayer> playersAndSpectators;

        public LobbyGame(string owner, StartNewGameRequest request)
        {
            Id = Guid.NewGuid();

            Owner = owner;
            Name = request.Name;
            Password = request.Password;
            ShowHand = request.ShowHand;
            SpectatorsCanWatch = request.Spectators;
            CreatedAt = DateTime.UtcNow;
            GameType = request.GameType;

            playersAndSpectators = new Dictionary<string, LobbyPlayer>();
            Messages = new List<string>();
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Owner { get; set; }
        public string Password { get; set; }
        public string GameType { get; set; }
        public bool ShowHand { get; set; }
        public bool SpectatorsCanWatch { get; set; }
        public bool Started { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Messages { get; set; }

        public bool HasPassword => !string.IsNullOrEmpty(Password);
        public Dictionary<string, LobbyPlayer> Players => playersAndSpectators.Where(kvp => !kvp.Value.IsSpectator).ToDictionary(key => key.Key, value => value.Value);

        public List<LobbyPlayer> GetSpectators()
        {
            return playersAndSpectators.Values.Where(p => p.IsSpectator).ToList();
        }

        public bool CanQuickJoin(string gameType)
        {
            return GameType.Equals(gameType, StringComparison.OrdinalIgnoreCase) && !Started && !HasPassword && Players.Count < 2;
        }

        public void NewGame(LobbyUser user)
        {
            var player = new LobbyPlayer
            {
                User = user,
                IsSpectator = false
            };

            playersAndSpectators.Add(user.Name, player);
        }

        public bool HasPlayer(string username)
        {
            return playersAndSpectators.ContainsKey(username);
        }

        public LobbyGameListSummary ToGameListSummary()
        {
            var summary = new LobbyGameListSummary();

            PopulateGameSummaryBase(summary);

            summary.GameType = GameType;
            summary.NeedsPassword = HasPassword;
            summary.ShowHand = ShowHand;
            summary.Players = new List<string>(Players.Values.Select(p => p.User.Name));

            return summary;
        }

        public LobbyGameSummary ToGameSummary()
        {
            var summary = new LobbyGameSummary();

            PopulateGameSummaryBase(summary);

            summary.Spectators = new List<string>(GetSpectators().Select(p => p.User.Name));
            summary.Players = Players.ToDictionary(k => k.Key, v => new LobbyPlayerSummary { Name = v.Value.User.Name, CustomData = v.Value.CustomData });
            summary.Messages = Messages;
            summary.Owner = Owner;

            return summary;
        }

        public void PlayerDisconnected(string username)
        {
            if (!HasPlayer(username))
            {
                return;
            }

            if (!Started)
            {
                // TODO add message
            }

            CheckAndResetOwner(username);

            playersAndSpectators.Remove(username);
        }

        public void PlayerLeave(string username)
        {
            if (!HasPlayer(username))
            {
                return;
            }

            if (!Started)
            {
                // TODO add message
            }

            CheckAndResetOwner(username);

            playersAndSpectators.Remove(username);
        }

        public GameResponse Join(LobbyUser user, string password)
        {
            if (playersAndSpectators.ContainsKey(user.Name))
            {
                return GameResponse.Failure("You are already in that game.");
            }

            if (Started)
            {
                return GameResponse.Failure("That game has already started.");
            }

            if (Players.Count >= 2)
            {
                return GameResponse.Failure("That game is full.");
            }

            if (!string.IsNullOrEmpty(Password) && Password != password)
            {
                return GameResponse.Failure("Incorrect password for that game.");
            }

            if (Players.Values.Any(player => player.User.HasUserBlocked(user) || user.HasUserBlocked(player.User)))
            {
                return GameResponse.Failure("You cannot join that game.");
            }

            // TODO Add message
            playersAndSpectators.Add(user.Name, new LobbyPlayer { User = user, IsSpectator = false });

            return GameResponse.Succeeded(this);
        }

        public bool IsEmpty()
        {
            return playersAndSpectators.Values.All(p => p.IsSpectator);
        }

        private void CheckAndResetOwner(string username)
        {
            if (Owner != username)
            {
                return;
            }

            var otherPlayer = playersAndSpectators.Values.FirstOrDefault(p => p.User.Name != username && !p.IsSpectator);
            if (otherPlayer != null)
            {
                Owner = otherPlayer.User.Name;
            }
        }

        private void PopulateGameSummaryBase(LobbyGameSummaryBase summary)
        {
            summary.Id = Id.ToString();
            summary.CreatedAt = CreatedAt;
            summary.Name = Name;
            summary.Started = Started;
        }
    }
}
