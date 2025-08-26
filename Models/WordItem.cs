namespace SpyGame.Models;

using SQLite;

public enum DifficultyLevel
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

public class WordItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int CategoryId { get; set; }

    // ایندکس یونیک ترکیبی روی CategoryId + Text
    [Indexed(Name = "UX_CategoryId_Text", Order = 1, Unique = true)]
    public int IndexedCategoryId => CategoryId;

    [Indexed(Name = "UX_CategoryId_Text", Order = 2, Unique = true)]
    public string Text { get; set; } = string.Empty;

    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;

}
