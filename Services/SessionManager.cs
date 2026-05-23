namespace SpyGame.Services;

public class SessionManager
{
    public int SpyScore { get; private set; }
    public int PlayerScore { get; private set; }
    public int RoundsPlayed { get; private set; }

    public void RecordSpyWin()
    {
        RoundsPlayed++;
        SpyScore += 2;
    }

    public void RecordPlayerWin()
    {
        RoundsPlayed++;
        PlayerScore += 1;
    }

    public void Reset()
    {
        SpyScore = 0;
        PlayerScore = 0;
        RoundsPlayed = 0;
    }
}
