namespace SpyGame.Models;

using SQLite;

public class Category
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "UX_Category_Name", Unique = true)]
    public string Name { get; set; } = string.Empty;

    // برای نمایش در UI
    public override string ToString() => Name;
}
