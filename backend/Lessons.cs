namespace ObyasniZadachu.Backend;

/// <summary>Тексты условий и решений. Числа берутся из ImageMath, здесь только слова.</summary>
public static class TaskText
{
    public static string UnitShort(Unit unit) => unit switch
    {
        Unit.Bits => "бит",
        Unit.Bytes => "байт",
        _ => "КиБ"
    };

    private static string UnitIn(Unit unit) => unit switch
    {
        Unit.Bits => "битах",
        Unit.Bytes => "байтах",
        _ => "килобайтах (1 КиБ = 1024 байта)"
    };

    public static string Condition(TaskSpec t)
    {
        if (t.Kind == TaskKind.Depth)
            return $"Палитра изображения содержит {t.Colors} цветов. " +
                   "Сколько бит нужно, чтобы закодировать цвет одного пикселя (какова глубина цвета)?";

        string color = t.Depth > 0 ? $"глубина цвета {t.Depth} бит" : $"палитра из {t.Colors} цветов";
        return $"Растровое изображение имеет размер {t.Width}×{t.Height} пикселей, {color}. " +
               $"Найдите его объём в {UnitIn(t.Unit)} (без сжатия и служебных данных). Ответ — число.";
    }

    /// <summary>Полное решение (показывается в песочнице после ответа).</summary>
    public static string[] Solution(TaskSpec t)
    {
        var lines = new List<string>();

        if (t.Kind == TaskKind.Depth)
        {
            int depth = ImageMath.DepthForColors(t.Colors);
            lines.Add($"Нужно наименьшее i, при котором 2^i ≥ {t.Colors}.");
            lines.Add($"2^{depth - 1} = {1L << (depth - 1)} — меньше {t.Colors}, не хватает кодов; " +
                      $"2^{depth} = {1L << depth} — хватает.");
            lines.Add($"Ответ: {depth} бит.");
            return lines.ToArray();
        }

        int d = ImageMath.DepthOf(t);
        long pixels = ImageMath.Pixels(t.Width, t.Height);
        long bits = ImageMath.TotalBits(t.Width, t.Height, d);

        lines.Add($"Пикселей: {t.Width} × {t.Height} = {pixels}.");
        lines.Add(t.Depth == 0
            ? $"Глубина цвета: наименьшее i, при котором 2^i ≥ {t.Colors}, то есть i = {d} бит."
            : $"Глубина цвета по условию: {d} бит.");
        lines.Add($"Всего бит: {pixels} × {d} = {bits}.");

        if (t.Unit != Unit.Bits)
            lines.Add($"В байтах: {bits} / 8 = {ImageMath.Format(ImageMath.InUnit(bits, Unit.Bytes))}.");
        if (t.Unit == Unit.KiB)
            lines.Add($"В КиБ: {ImageMath.Format(ImageMath.InUnit(bits, Unit.Bytes))} / 1024 = " +
                      $"{ImageMath.Format(ImageMath.InUnit(bits, Unit.KiB))}.");

        lines.Add($"Ответ: {ImageMath.Format(ImageMath.Expected(t))} {UnitShort(t.Unit)}.");
        return lines.ToArray();
    }
}

/// <summary>Фиксированные задачи страницы: «Задача 1», разбор ошибки, «Задача с ошибкой».</summary>
public static class Lessons
{
    /// <summary>Задача 1: палитра 200 цветов — не степень двойки, глубина 8 бит.</summary>
    public static readonly TaskSpec Task1 = new(TaskKind.Volume, 64, 32, 200, 0, Unit.Bytes);

    /// <summary>Разбор ошибочного решения: ученик округлил глубину вниз.</summary>
    public static readonly TaskSpec MistakeExample = new(TaskKind.Volume, 40, 25, 100, 0, Unit.Bytes);

    /// <summary>Задача с ошибкой: на шаге 4 ученик умножил на 8 вместо деления.</summary>
    public static readonly TaskSpec FindErrorTask = new(TaskKind.Volume, 100, 40, 16, 0, Unit.Bytes);

    public const int FindErrorStepNumber = 4;

    public static readonly string[] FindErrorSteps =
    {
        "Пикселей: 100 × 40 = 4000.",
        "Глубина цвета: 16 цветов = 2^4, значит i = 4 бита.",
        "Всего бит: 4000 × 4 = 16000 бит.",
        "Перевод в байты: 16000 × 8 = 128000 байт.",
        "Ответ: 128000 байт."
    };

    /// <summary>Решение «Задачи 1»: показывается после нескольких неверных попыток.</summary>
    public static string[] Task1Solution() => TaskText.Solution(Task1);

    public static LessonsOverview Overview() => new(
        new GeneratedTask(Task1, TaskText.Condition(Task1)),
        new MistakeTaskView(TaskText.Condition(FindErrorTask), FindErrorSteps));
}
