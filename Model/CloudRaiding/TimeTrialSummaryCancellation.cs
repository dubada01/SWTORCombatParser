using System.Threading;
using System.Threading.Tasks;

namespace SWTORCombatParser.Model.CloudRaiding;

public static partial class API_Connection
{
    public static Task<TimeTrialSummary> GetTimeTrialSummary(string boss, string encounter, string difficulty,
        string players, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return GetTimeTrialSummary(boss, encounter, difficulty, players).WaitAsync(cancellationToken);
    }
}
