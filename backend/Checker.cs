namespace ObyasniZadachu.Backend;

/// <summary>
/// Чекер: сравнивает ответ с правильным и, если ответ неверен,
/// пытается узнать типичную ошибку и объяснить её.
/// Ничего не хранит: правильный ответ каждый раз пересчитывается из условия.
/// </summary>
public static class Checker
{
    public static CheckResult Check(TaskSpec task, string? answer)
    {
        var error = ImageMath.Validate(task);
        if (error != null)
            return new CheckResult(CheckStatus.InvalidInput, error);

        if (!ImageMath.TryParseAnswer(answer, out var value))
            return new CheckResult(CheckStatus.InvalidInput, "Введите ответ числом, например 2048 или 0,25.");

        var expected = ImageMath.Expected(task);
        if (ImageMath.Matches(value, expected))
        {
            var unit = task.Kind == TaskKind.Depth ? Unit.Bits : task.Unit;
            return new CheckResult(CheckStatus.Correct,
                $"Верно! Ответ: {ImageMath.Format(expected)} {TaskText.UnitShort(unit)}.");
        }

        string why = task.Kind == TaskKind.Depth
            ? ExplainDepthMistake(task, value)
            : ExplainVolumeMistake(task, value);

        return new CheckResult(CheckStatus.Wrong, "Неверно. " + why);
    }

    /// <summary>Проверка задачи «найди ошибку»: номер шага и правильный ответ.</summary>
    public static CheckResult CheckMistake(MistakeAnswer a)
    {
        if (a.Step < 1 || a.Step > Lessons.FindErrorSteps.Length)
            return new CheckResult(CheckStatus.InvalidInput, "Выберите номер шага с ошибкой.");

        if (!ImageMath.TryParseAnswer(a.Answer, out var value))
            return new CheckResult(CheckStatus.InvalidInput, "Введите правильный ответ числом.");

        var expected = ImageMath.Expected(Lessons.FindErrorTask);
        bool stepOk = a.Step == Lessons.FindErrorStepNumber;
        bool answerOk = value == expected;

        if (stepOk && answerOk)
            return new CheckResult(CheckStatus.Correct,
                $"Верно! Ошибка в шаге {Lessons.FindErrorStepNumber}: биты переводят в байты делением на 8, " +
                $"а не умножением (байтов всегда меньше, чем бит). Правильный ответ: {ImageMath.Format(expected)} байт.");

        if (stepOk)
            return new CheckResult(CheckStatus.Wrong,
                "Шаг с ошибкой найден верно, но ответ не совпал. Пересчитайте этот шаг правильно.");

        if (answerOk)
            return new CheckResult(CheckStatus.Wrong,
                "Ответ верный, но ошибка не в этом шаге. Ищите первый шаг, где вычисление неверно.");

        return new CheckResult(CheckStatus.Wrong,
            "Ни шаг, ни ответ не совпали. Проверяйте шаги по порядку и остановитесь на первом неверном: " +
            "последующие шаги могут быть лишь следствием.");
    }

    // ---------- Разбор типичных ошибок ----------

    public static string ExplainDepthMistake(TaskSpec t, decimal value)
    {
        long n = t.Colors;
        int depth = ImageMath.DepthForColors(n);

        if (value <= 0 || value != decimal.Truncate(value))
            return "Глубина цвета — целое положительное число бит.";

        if (value == n || value == n - 1)
            return "Вы назвали число цветов (или номер последнего цвета). Нужно число бит, которым записывается номер цвета.";

        if (value >= 64)
            return "Это слишком много бит. Вспомните: k бит дают 2^k разных кодов.";

        int k = (int)value;

        if (k < depth)
        {
            if (k == depth - 1 && !ImageMath.IsPowerOfTwo(n))
                return $"Похоже, вы округлили вниз. {n} — не степень двойки: 2^{k} = {1L << k} < {n}, " +
                       "кодов на все цвета не хватит. Округлять нужно вверх.";
            return $"2^{k} = {1L << k} — меньше {n}, разных кодов на все цвета не хватит.";
        }

        return $"{k} бит хватает, но это не минимум: нужно наименьшее i, при котором 2^i ≥ {n}.";
    }

    public static string ExplainVolumeMistake(TaskSpec t, decimal value)
    {
        long pixels = ImageMath.Pixels(t.Width, t.Height);
        int depth = ImageMath.DepthOf(t);
        long bits = ImageMath.TotalBits(t.Width, t.Height, depth);
        string unit = TaskText.UnitShort(t.Unit);

        var hints = new List<(decimal Value, string Text)>
        {
            (pixels, "Это только число пикселей. Объём — число пикселей, умноженное на глубину цвета.")
        };

        switch (t.Unit)
        {
            case Unit.Bits:
                hints.Add((bits / 8m, "Это байты, а нужны биты: 1 байт = 8 бит."));
                break;
            case Unit.Bytes:
                hints.Add((bits, "Это объём в битах. Переведите в байты: 1 байт = 8 бит, делим на 8."));
                hints.Add((bits * 8m, "Вы умножили на 8, а биты переводят в байты делением на 8."));
                hints.Add((bits / 8192m, "Это килобайты (КиБ), а нужны байты."));
                break;
            case Unit.KiB:
                hints.Add((bits, "Это объём в битах. Переведите в КиБ: сначала делим на 8, потом на 1024."));
                hints.Add((bits / 8m, "Это байты. Осталось перевести их в КиБ: разделить на 1024."));
                hints.Add((bits / 8000m, "Вы делили на 1000, а в 1 КиБ — 1024 байта."));
                break;
        }

        if (t.Depth == 0)
        {
            if (!ImageMath.IsPowerOfTwo(t.Colors) && depth > 1)
                hints.Add((ImageMath.InUnit(pixels * (depth - 1), t.Unit),
                    $"Похоже, глубину цвета округлили вниз. Для {t.Colors} цветов нужно {depth} бит, потому что 2^{depth - 1} < {t.Colors}."));

            hints.Add((ImageMath.InUnit(pixels * t.Colors, t.Unit),
                "Вы умножили на число цветов. Умножать нужно на глубину цвета — число бит на один пиксель."));
        }

        foreach (var hint in hints)
            if (hint.Value == value)
                return hint.Text;

        return $"Проверьте цепочку: пиксели → глубина цвета → всего бит → перевод в {unit}.";
    }
}
