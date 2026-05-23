using SQLite;

namespace SpyGame.Models;

public class GameResult
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public DateTime PlayedOn { get; set; } = DateTime.UtcNow;
    public int Players { get; set; }
    public int Spies { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string SecretWord { get; set; } = string.Empty;

    public bool SpyWon { get; set; }
    public bool SpyCaught { get; set; }
    public bool SpyGuessedCorrectly { get; set; }
}
