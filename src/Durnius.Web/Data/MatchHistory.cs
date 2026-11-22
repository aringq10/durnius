namespace Durnius.Web.Data;

public class MatchHistory
{
    public int Id { get; set; }
    public string GameType { get; set; } = string.Empty;
    public DateTime MatchDate { get; set; } = DateTime.UtcNow;

    public List<MatchParticipant> Participants { get; set; } = new();
}
