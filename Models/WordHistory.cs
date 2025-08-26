using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpyGame.Models;
public class WordHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int WordItemId { get; set; }
}
