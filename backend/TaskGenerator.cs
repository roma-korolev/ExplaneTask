namespace ObyasniZadachu.Backend;

/// <summary>
/// Генератор задач для песочницы: два типа задач со случайными числами.
/// Числа подбираются так, чтобы ответ был целым (удобно вводить и проверять).
/// </summary>
public static class TaskGenerator
{
    private static readonly int[] Depths = { 1, 2, 3, 4, 5, 6, 8, 16, 24 };

    /// <param name="kind">"depth", "volume" или что угодно другое — тогда тип случайный.</param>
    public static GeneratedTask Generate(string? kind, Random? random = null)
    {
        random ??= Random.Shared;

        var taskKind = kind?.ToLowerInvariant() switch
        {
            "depth" => TaskKind.Depth,
            "volume" => TaskKind.Volume,
            _ => random.Next(2) == 0 ? TaskKind.Depth : TaskKind.Volume
        };

        var spec = taskKind == TaskKind.Depth ? DepthTask(random) : VolumeTask(random);
        return new GeneratedTask(spec, TaskText.Condition(spec));
    }

    /// <summary>Тип 1: глубина цвета по размеру палитры. Половина палитр — степени двойки, половина — нет.</summary>
    private static TaskSpec DepthTask(Random random)
    {
        long colors;
        if (random.Next(2) == 0)
        {
            colors = 1L << random.Next(1, 17);          // 2, 4, ..., 65536
        }
        else
        {
            do colors = random.Next(3, 5001);
            while (ImageMath.IsPowerOfTwo(colors));     // особый случай: округление вверх
        }

        return new TaskSpec(TaskKind.Depth, 0, 0, colors, 0, Unit.Bits);
    }

    /// <summary>
    /// Тип 2: объём по размерам и глубине цвета.
    /// Стороны кратны 4 (для байтов) или 16 (для КиБ); перебор идёт, пока ответ не станет целым.
    /// </summary>
    private static TaskSpec VolumeTask(Random random)
    {
        for (int attempt = 0; attempt < 500; attempt++)
        {
            var unit = (Unit)random.Next(3);
            int depth = Depths[random.Next(Depths.Length)];
            int step = unit == Unit.KiB ? 16 : 4;
            int maxMultiplier = unit == Unit.KiB ? 32 : 16;

            int width = step * random.Next(1, maxMultiplier + 1);
            int height = step * random.Next(1, maxMultiplier + 1);

            long bits = ImageMath.TotalBits(width, height, depth);
            decimal value = ImageMath.InUnit(bits, unit);
            if (value == decimal.Truncate(value))
                return new TaskSpec(TaskKind.Volume, width, height, 0, depth, unit);
        }

        return new TaskSpec(TaskKind.Volume, 64, 32, 0, 8, Unit.Bytes);   // запасной вариант
    }
}
