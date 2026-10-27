namespace Durnius.Web.Data;

public class MatchParticipant
{
    public int MatchHistoryId { get; set; }
    public MatchHistory MatchHistory { get; set; } = null!;
    
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    
    public int? Placement { get; set; }
}