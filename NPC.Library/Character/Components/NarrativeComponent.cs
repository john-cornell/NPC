namespace NPC.Library.Character.Components;

using System.Collections.Generic;

public class NarrativeRecord
{
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class NarrativeComponent
{
    private readonly Queue<NarrativeRecord> _history = new Queue<NarrativeRecord>();
    private const int MaxHistory = 10;

    public void AddRecord(string action, string reason)
    {
        _history.Enqueue(new NarrativeRecord { Action = action, Reason = reason });
        if (_history.Count > MaxHistory)
        {
            _history.Dequeue();
        }
    }

    public IEnumerable<NarrativeRecord> GetHistory()
    {
        return _history;
    }
}
