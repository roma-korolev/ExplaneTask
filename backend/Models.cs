namespace ObyasniZadachu.Backend;

// ---------- Задачи ----------

public enum TaskKind { Depth, Volume }          // тип 1: глубина по палитре; тип 2: объём
public enum Unit { Bits, Bytes, KiB }           // единица измерения ответа
public enum CheckStatus { InvalidInput, Correct, Wrong }

/// <summary>
/// Условие задачи. Одна и та же запись описывает и пример, и задачу для решения.
/// Depth = 0 означает «глубина не дана, её надо вывести из палитры (Colors)».
/// </summary>
public record TaskSpec(TaskKind Kind, int Width, int Height, long Colors, int Depth, Unit Unit);

public record GeneratedTask(TaskSpec Spec, string Text);

// ---------- Проверка ответа (Чекер) ----------

public record CheckRequest(TaskSpec Task, string? Answer, string? ClientId, bool Sandbox);
public record CheckResult(CheckStatus Status, string Message);
public record CheckResponse(CheckResult Result, StatsSnapshot? Stats, string[]? Solution);

public record MistakeAnswer(int Step, string? Answer);

// ---------- Кнопки и шаги ----------

/// <summary>Section: "main" или "mistake". Action: "next" или "reset".</summary>
public record ButtonRequest(string Section, string Action, int Step, int Width, int Height, long Colors);

/// <summary>Kind: "number" (поле ввода) или "choice" (выбор из Options).</summary>
public record StepQuestion(string Prompt, string Hint, string Kind, string[]? Options);

public record StepView(
    int Step,
    int StepCount,
    string Title,
    string Context,
    string Text,
    StepQuestion? Question,
    string? Error)
{
    public static StepView Failed(string error) => new(0, 0, "", "", "", null, error);
}

public record StepAnswerRequest(string Section, int Step, int Width, int Height, long Colors, string? Answer);
public record StepCheckResponse(CheckStatus Status, string Message, string? Solution);

// ---------- «Предскажите» ----------

public record PredictRequest(int Width, int Height, long Colors, string Change, string? Answer);
public record PredictResponse(CheckResult Result, bool Applied, int Width, int Height, long Colors);

// ---------- Визуализация (сетка пикселей, палитра) ----------

public record DescribeRequest(int Width, int Height, long Colors, int PixelCol, int PixelRow);

public record Description(
    string? Error,
    int Depth,
    long Pixels,
    long TotalBits,
    string Bytes,
    string Kib,
    int Cols,
    int Rows,
    long[] Cells,
    int SelectedCol,
    int SelectedRow,
    long SelectedColorIndex,
    string SelectedCode,
    string[] PaletteCodes)
{
    public static Description Failed(string error) =>
        new(error, 0, 0, 0, "", "", 0, 0, Array.Empty<long>(), 0, 0, 0, "", Array.Empty<string>());
}

// ---------- Готовые уроки ----------

public record MistakeTaskView(string Condition, string[] Steps);
public record LessonsOverview(GeneratedTask Task1, MistakeTaskView FindError);

// ---------- Картинки ----------

public record ImageInfoResult(int Width, int Height, long Colors, string Note);

public record ImageResponse(string? Error, ImageInfoResult? Info, GeneratedTask? DepthTask, GeneratedTask? VolumeTask)
{
    public static ImageResponse Fail(string error) => new(error, null, null, null);
}

// ---------- Статистика ----------

public record StatsSnapshot(
    int Total,
    int Correct,
    double TotalPercent,
    int Last10Count,
    int Last10Correct,
    double Last10Percent)
{
    public static readonly StatsSnapshot Empty = new(0, 0, 0, 0, 0, 0);
}
