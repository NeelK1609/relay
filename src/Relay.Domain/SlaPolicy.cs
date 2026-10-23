namespace Relay.Domain;

public static class SlaPolicy
{
    public static (TimeSpan FirstResponse, TimeSpan Resolution) For(CasePriority priority) => priority switch
    {
        CasePriority.Urgent => (TimeSpan.FromHours(1), TimeSpan.FromHours(8)),
        CasePriority.High => (TimeSpan.FromHours(4), TimeSpan.FromHours(24)),
        CasePriority.Normal => (TimeSpan.FromHours(8), TimeSpan.FromHours(72)),
        CasePriority.Low => (TimeSpan.FromHours(24), TimeSpan.FromHours(120)),
        _ => throw new ArgumentOutOfRangeException(nameof(priority)),
    };
}
