using System.Collections.Concurrent;

namespace ObyasniZadachu.Backend;

/// <summary>
/// Статистика песочницы: процент верных ответов за всё время и за последние 10 задач.
/// Хранится в памяти сервера отдельно для каждого браузера (идентификатор лежит в localStorage страницы).
/// При перезагрузке страницы статистика сохраняется, при перезапуске сервера — обнуляется.
/// </summary>
public class StatisticsService
{
    private const int MaxClients = 10_000;
    private readonly ConcurrentDictionary<string, ClientStats> _clients = new();

    public static bool IsValidClientId(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 64 && id.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');

    public StatsSnapshot Get(string clientId) =>
        _clients.TryGetValue(clientId, out var stats) ? stats.Snapshot() : StatsSnapshot.Empty;

    public StatsSnapshot Record(string clientId, bool correct)
    {
        if (!_clients.TryGetValue(clientId, out var stats))
        {
            if (_clients.Count >= MaxClients) return StatsSnapshot.Empty;   // защита памяти
            stats = _clients.GetOrAdd(clientId, _ => new ClientStats());
        }

        return stats.Add(correct);
    }
}

public class ClientStats
{
    private readonly object _lock = new();
    private readonly Queue<bool> _last10 = new();
    private int _total;
    private int _correct;

    public StatsSnapshot Add(bool correct)
    {
        lock (_lock)
        {
            _total++;
            if (correct) _correct++;

            _last10.Enqueue(correct);
            if (_last10.Count > 10) _last10.Dequeue();

            return Snapshot();
        }
    }

    public StatsSnapshot Snapshot()
    {
        lock (_lock)
        {
            int last10Correct = _last10.Count(x => x);
            return new StatsSnapshot(
                _total,
                _correct,
                Percent(_correct, _total),
                _last10.Count,
                last10Correct,
                Percent(last10Correct, _last10.Count));
        }
    }

    private static double Percent(int part, int whole) =>
        whole == 0 ? 0 : Math.Round(100.0 * part / whole, 1);
}
