namespace ObyasniZadachu.Backend;

/// <summary>
/// Обработчик кнопок. Принимает действие пользователя («Следующий шаг», «Сначала», смена чисел,
/// ответ на вопрос внутри шага) и возвращает то, что нужно показать на экране.
///
/// Сервер состояния не держит: страница присылает номер шага и числа с каждым запросом,
/// а правильный ответ каждый раз пересчитывается заново. Поэтому при перезагрузке страницы
/// разбор начинается сначала, а смена чисел на странице отправляет «reset».
///
/// Каждый шаг разбора содержит вопрос. Страница открывает «Следующий шаг» только после того,
/// как сервер (CheckStep) ответил «Correct».
/// </summary>
public static class ButtonHandler
{
    private const int ShowMaxPixels = 256;  // на экране не больше 256 пикселей
    private const int ShowMaxSide = 16;     // большая картинка: показываем левый верхний угол 16×16

    // ---------- Описание шага разбора ----------

    /// <param name="Expected">Правильный ответ (число; для выбора — номер варианта).</param>
    /// <param name="Solution">Разбор шага — показывается после верного ответа.</param>
    /// <param name="Explain">Объяснение неверного ответа.</param>
    private sealed record Step(
        string Title,
        string Text,
        StepQuestion Question,
        decimal Expected,
        string Solution,
        Func<decimal, string> Explain);

    private sealed record Lesson(string Context, Step[] Steps);

    private static readonly string[] YesNo = { "Верно", "Есть ошибка" };

    // ---------- Визуализация ----------

    public static Description Describe(DescribeRequest r)
    {
        var error = ImageMath.ValidateSize(r.Width, r.Height) ?? ImageMath.ValidateColors(r.Colors);
        if (error != null) return Description.Failed(error);

        int depth = ImageMath.DepthForColors(r.Colors);
        long pixels = ImageMath.Pixels(r.Width, r.Height);
        long bits = ImageMath.TotalBits(r.Width, r.Height, depth);

        // Маленькое изображение (до 256 пикселей) показываем целиком, большое — левый верхний угол
        bool small = (long)r.Width * r.Height <= ShowMaxPixels;
        int cols = small ? r.Width : Math.Min(r.Width, ShowMaxSide);
        int rows = small ? r.Height : Math.Min(r.Height, ShowMaxSide);

        var cells = new long[cols * rows];
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < cols; col++)
                cells[row * cols + col] = ImageMath.ColorIndexOfPixel((long)row * r.Width + col, r.Colors);

        int selCol = Math.Clamp(r.PixelCol, 0, cols - 1);
        int selRow = Math.Clamp(r.PixelRow, 0, rows - 1);
        long selColor = cells[selRow * cols + selCol];

        int paletteShown = (int)Math.Min(r.Colors, 16);
        var codes = new string[paletteShown];
        for (int i = 0; i < paletteShown; i++)
            codes[i] = ImageMath.ToBinary(i, depth);

        return new Description(
            Error: null,
            Depth: depth,
            Pixels: pixels,
            TotalBits: bits,
            Bytes: ImageMath.Format(ImageMath.InUnit(bits, Unit.Bytes)),
            Kib: ImageMath.Format(ImageMath.InUnit(bits, Unit.KiB)),
            Cols: cols,
            Rows: rows,
            Cells: cells,
            SelectedCol: selCol,
            SelectedRow: selRow,
            SelectedColorIndex: selColor,
            SelectedCode: ImageMath.ToBinary(selColor, depth),
            PaletteCodes: codes);
    }

    // ---------- «Предскажите»: изменить один параметр ----------

    public static PredictResponse Predict(PredictRequest r)
    {
        var error = ImageMath.ValidateSize(r.Width, r.Height) ?? ImageMath.ValidateColors(r.Colors);
        if (error != null)
            return Predicted(CheckStatus.InvalidInput, error, r.Width, r.Height, r.Colors);

        int w = r.Width;
        int h = r.Height;
        long n = r.Colors;

        try
        {
            checked
            {
                switch (r.Change)
                {
                    case "width": w *= 2; break;
                    case "both": w *= 2; h *= 2; break;
                    case "colors2": n *= 2; break;
                    case "colorsSquare": n *= n; break;
                    default:
                        return Predicted(CheckStatus.InvalidInput, "Неизвестное изменение.", r.Width, r.Height, r.Colors);
                }
            }
        }
        catch (OverflowException)
        {
            return Predicted(CheckStatus.InvalidInput,
                "После такого изменения числа получатся слишком большими. Выберите другое изменение или уменьшите числа.",
                r.Width, r.Height, r.Colors);
        }

        var newError = ImageMath.ValidateSize(w, h) ?? ImageMath.ValidateColors(n);
        if (newError != null)
            return Predicted(CheckStatus.InvalidInput,
                "После такого изменения числа выйдут за допустимые границы. " + newError +
                " Выберите другое изменение или уменьшите числа.",
                r.Width, r.Height, r.Colors);

        var spec = new TaskSpec(TaskKind.Volume, w, h, n, 0, Unit.Bits);
        var result = Checker.Check(spec, r.Answer);

        return result.Status == CheckStatus.Correct
            ? new PredictResponse(result, true, w, h, n)
            : new PredictResponse(result, false, r.Width, r.Height, r.Colors);
    }

    private static PredictResponse Predicted(CheckStatus status, string message, int w, int h, long n) =>
        new(new CheckResult(status, message), false, w, h, n);

    // ---------- Пошаговые разборы ----------

    /// <summary>Кнопки «Следующий шаг» и «Сначала».</summary>
    public static StepView Handle(ButtonRequest r)
    {
        if (r.Action is not ("reset" or "next"))
            return StepView.Failed("Неизвестное действие.");

        var lesson = Build(r.Section, r.Width, r.Height, r.Colors, out var error);
        if (lesson is null)
            return StepView.Failed(error ?? "Неизвестный раздел.");

        int index = r.Action == "reset" ? 0 : Math.Clamp(r.Step + 1, 0, lesson.Steps.Length - 1);
        var step = lesson.Steps[index];

        return new StepView(index, lesson.Steps.Length, step.Title, lesson.Context, step.Text, step.Question, null);
    }

    /// <summary>Проверка ответа на вопрос шага. Правильный ответ пересчитывается из условия.</summary>
    public static StepCheckResponse CheckStep(StepAnswerRequest r)
    {
        var lesson = Build(r.Section, r.Width, r.Height, r.Colors, out var error);
        if (lesson is null)
            return new StepCheckResponse(CheckStatus.InvalidInput, error ?? "Неизвестный раздел.", null);

        if (r.Step < 0 || r.Step >= lesson.Steps.Length)
            return new StepCheckResponse(CheckStatus.InvalidInput, "Неверный номер шага.", null);

        var step = lesson.Steps[r.Step];

        if (!ImageMath.TryParseAnswer(r.Answer, out var value))
        {
            string hint = step.Question.Kind == "choice"
                ? "Выберите один из вариантов."
                : "Введите ответ числом, например 2048 или 0,25.";
            return new StepCheckResponse(CheckStatus.InvalidInput, hint, null);
        }

        if (ImageMath.Matches(value, step.Expected))
            return new StepCheckResponse(CheckStatus.Correct, "Верно!", step.Solution);

        return new StepCheckResponse(CheckStatus.Wrong, "Неверно. " + step.Explain(value), null);
    }

    private static Lesson? Build(string section, int width, int height, long colors, out string? error)
    {
        error = null;
        switch (section)
        {
            case "main":
                return BuildMain(width, height, colors, out error);
            case "mistake":
                return BuildMistake();
            default:
                error = "Неизвестный раздел.";
                return null;
        }
    }

    // ---------- Основной пример: числа задаёт пользователь ----------

    private static Lesson? BuildMain(int w, int h, long n, out string? error)
    {
        error = ImageMath.ValidateSize(w, h) ?? ImageMath.ValidateColors(n);
        if (error != null) return null;

        int depth = ImageMath.DepthForColors(n);
        long pixels = ImageMath.Pixels(w, h);
        long bits = ImageMath.TotalBits(w, h, depth);
        decimal bytes = ImageMath.InUnit(bits, Unit.Bytes);
        decimal kib = ImageMath.InUnit(bits, Unit.KiB);

        var depthSpec = new TaskSpec(TaskKind.Depth, w, h, n, 0, Unit.Bits);
        var bitsSpec = new TaskSpec(TaskKind.Volume, w, h, n, 0, Unit.Bits);
        var bytesSpec = new TaskSpec(TaskKind.Volume, w, h, n, 0, Unit.Bytes);
        var kibSpec = new TaskSpec(TaskKind.Volume, w, h, n, 0, Unit.KiB);

        string notPower = ImageMath.IsPowerOfTwo(n)
            ? ""
            : $"\nЧисло {n} — не степень двойки, поэтому глубину округляем вверх: часть кодов останется неиспользованной.";

        string context =
            $"Задача: растровое изображение {w}×{h} пикселей, палитра из {n} цветов. " +
            "Найдите его объём в битах, байтах и КиБ (без сжатия и служебных данных).";

        var steps = new[]
        {
            new Step(
                "Сколько пикселей",
                "План: 1) число пикселей; 2) глубина цвета; 3) объём в битах; 4) в байтах; 5) в КиБ. " +
                "Начнём с размера сетки: она состоит из строк, в каждой строке — одинаковое число пикселей.",
                new StepQuestion(
                    $"Сколько всего пикселей в изображении {w}×{h}?",
                    "Сетка 4×3 — это 12 пикселей. Умножьте ширину на высоту.",
                    "number", null),
                pixels,
                $"Пикселей: {w} × {h} = {pixels}.",
                v => v == w + h
                    ? "Вы сложили стороны. Число пикселей — произведение ширины на высоту."
                    : v == 2L * (w + h)
                        ? "Это периметр изображения. Пикселей столько, сколько клеток в сетке: ширина × высота."
                        : $"Пикселей столько, сколько клеток в сетке: {w} × {h}."),

            new Step(
                "Глубина цвета",
                "Каждому пикселю нужен код цвета — номер цвета в палитре. Все коды одной длины. " +
                "Число бит в коде называется глубиной цвета i. i бит дают 2^i разных кодов.",
                new StepQuestion(
                    $"Сколько бит нужно, чтобы записать номер любого из {n} цветов?",
                    "Найдите наименьшее i, при котором 2^i ≥ " + n + ". Если число цветов не степень двойки — округляйте вверх.",
                    "number", null),
                depth,
                $"Ищем наименьшее i, при котором 2^i ≥ {n}.\n" +
                $"2^{depth - 1} = {1L << (depth - 1)} < {n} — кодов не хватает.\n" +
                $"2^{depth} = {1L << depth} ≥ {n} — кодов хватает.\n" +
                $"Глубина цвета i = {depth} бит." + notPower,
                v => Checker.ExplainDepthMistake(depthSpec, v)),

            new Step(
                "Объём в битах",
                "Каждый пиксель занимает i бит. Значит, объём изображения = число пикселей × глубина цвета.",
                new StepQuestion(
                    $"Сколько бит занимает всё изображение: {pixels} пикселей по {depth} бит?",
                    $"Умножьте {pixels} на {depth}.",
                    "number", null),
                bits,
                $"{pixels} × {depth} = {bits} бит.",
                v => Checker.ExplainVolumeMistake(bitsSpec, v)),

            new Step(
                "Перевод в байты",
                "Объём часто просят в байтах. В 1 байте 8 бит, поэтому биты делят на 8 " +
                "(байтов всегда меньше, чем бит). Если число не делится на 8, получится дробное число байт — это нормально.",
                new StepQuestion(
                    $"Сколько это байт ({bits} бит)?",
                    $"Разделите {bits} на 8.",
                    "number", null),
                bytes,
                $"{bits} / 8 = {ImageMath.Format(bytes)} байт.",
                v => Checker.ExplainVolumeMistake(bytesSpec, v)),

            new Step(
                "Перевод в КиБ",
                "Большие объёмы записывают в килобайтах (КиБ). В 1 КиБ 1024 байта, поэтому байты делят на 1024.",
                new StepQuestion(
                    $"Сколько это КиБ ({ImageMath.Format(bytes)} байт)?",
                    $"Разделите {ImageMath.Format(bytes)} на 1024. Если число получилось не целым — запишите его до 6 знаков после запятой или округлите до сотых.",
                    "number", null),
                kib,
                $"{ImageMath.Format(bytes)} / 1024 = {ImageMath.Format(kib)} КиБ.\n" +
                $"Итог: {bits} бит = {ImageMath.Format(bytes)} байт = {ImageMath.Format(kib)} КиБ.",
                v => Checker.ExplainVolumeMistake(kibSpec, v))
        };

        return new Lesson(context, steps);
    }

    // ---------- Разбор чужого неверного решения ----------

    private static Lesson BuildMistake()
    {
        var t = Lessons.MistakeExample;
        int depth = ImageMath.DepthOf(t);               // правильная глубина
        int wrongDepth = depth - 1;                     // ученик округлил вниз
        long pixels = ImageMath.Pixels(t.Width, t.Height);
        long wrongBits = ImageMath.TotalBits(t.Width, t.Height, wrongDepth);
        long rightBits = ImageMath.TotalBits(t.Width, t.Height, depth);
        decimal wrongBytes = ImageMath.InUnit(wrongBits, Unit.Bytes);
        decimal rightBytes = ImageMath.InUnit(rightBits, Unit.Bytes);

        string context =
            $"Задача: изображение {t.Width}×{t.Height} пикселей, палитра из {t.Colors} цветов. Найти объём в байтах. " +
            "Ниже по одной строке показано решение ученика. Для каждой строки решите: она верна или в ней ошибка.";

        var steps = new[]
        {
            new Step(
                "Строка ученика 1",
                $"Ученик написал:\n«Пикселей: {t.Width} × {t.Height} = {pixels}.»",
                new StepQuestion("Верна ли эта строка?", "Проверьте умножение сами.", "choice", YesNo),
                0,
                $"Да, {t.Width} × {t.Height} = {pixels}. Первая строка верна.",
                _ => $"Проверьте сами: {t.Width} × {t.Height} = {pixels}. Здесь ошибки нет."),

            new Step(
                "Строка ученика 2",
                $"Ученик написал:\n«Глубина цвета: log₂({t.Colors}) ≈ 6,64, значит i = {wrongDepth} бит.»",
                new StepQuestion("Верна ли эта строка?",
                    $"Хватит ли {wrongDepth} бит, чтобы записать {t.Colors} разных цветов? Вспомните: i бит дают 2^i кодов.",
                    "choice", YesNo),
                1,
                $"Ошибка! 2^{wrongDepth} = {1L << wrongDepth} < {t.Colors}: {wrongDepth} битами можно закодировать только " +
                $"{1L << wrongDepth} цветов, а нужно {t.Colors}. Дробного числа бит не бывает, поэтому результат округляют " +
                $"вверх, а не вниз: 2^{depth} = {1L << depth} ≥ {t.Colors}, значит i = {depth} бит.",
                _ => $"Проверьте: 2^{wrongDepth} = {1L << wrongDepth}. Хватает ли этого для {t.Colors} цветов?"),

            new Step(
                "Строка ученика 3",
                $"Ученик написал:\n«Всего бит: {pixels} × {wrongDepth} = {wrongBits}.»",
                new StepQuestion("Верно ли выполнено само умножение (считая, что i = " + wrongDepth + ")?",
                    "Ошибка из прошлой строки не делает неверным умножение: проверяйте только эту строку.",
                    "choice", YesNo),
                0,
                $"{pixels} × {wrongDepth} = {wrongBits} — умножение выполнено верно, но использует неверную глубину " +
                "из строки 2. Так одна ошибка тянется дальше по решению.",
                _ => $"Посчитайте: {pixels} × {wrongDepth} = {wrongBits}. Умножение верное; ошибка осталась в прошлой строке."),

            new Step(
                "Строка ученика 4",
                $"Ученик написал:\n«В байтах: {wrongBits} / 8 = {ImageMath.Format(wrongBytes)}. Ответ: {ImageMath.Format(wrongBytes)} байт.»",
                new StepQuestion("Верно ли выполнен перевод из бит в байты?",
                    "Биты в байты переводят делением на 8. Проверьте деление.",
                    "choice", YesNo),
                0,
                $"{wrongBits} / 8 = {ImageMath.Format(wrongBytes)} — перевод выполнен верно. Но итоговый ответ " +
                $"{ImageMath.Format(wrongBytes)} байт неверен — только из-за ошибки в строке 2.",
                _ => $"Посчитайте: {wrongBits} / 8 = {ImageMath.Format(wrongBytes)}. Перевод верный."),

            new Step(
                "Правильное решение",
                $"Теперь исправим ошибку в строке 2: глубина цвета i = {depth} бит. Остальное пересчитаем.",
                new StepQuestion($"Какой правильный объём изображения в байтах?",
                    $"Пикселей {pixels}, глубина {depth} бит. Умножьте, потом разделите на 8.",
                    "number", null),
                rightBytes,
                $"Глубина цвета: 2^{depth - 1} = {1L << (depth - 1)} < {t.Colors} ≤ 2^{depth} = {1L << depth}, значит i = {depth} бит.\n" +
                $"Всего бит: {pixels} × {depth} = {rightBits}. В байтах: {rightBits} / 8 = {ImageMath.Format(rightBytes)}.\n" +
                "Правило: если число цветов не степень двойки, глубину цвета округляют вверх.",
                v => Checker.ExplainVolumeMistake(t, v))
        };

        return new Lesson(context, steps);
    }
}