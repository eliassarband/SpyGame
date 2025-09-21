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

    [Indexed] // فقط ایندکس معمولی کافیست
    public int CategoryId { get; set; }

    // ایندکس ترکیبی را "غیر Unique" کن یا کلاً حذفش کن
    [Indexed(Name = "IX_CategoryId_Text", Order = 1, Unique = false)]
    public int IndexedCategoryId => CategoryId;

    [Indexed(Name = "IX_CategoryId_Text", Order = 2, Unique = false)]
    public string Text { get; set; } = string.Empty;

    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Medium;
}

