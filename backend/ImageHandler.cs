namespace ObyasniZadachu.Backend;

/// <summary>
/// Обработчик картинок. Браузер открывает файл любого привычного формата и присылает размеры
/// и пиксели (по 3 байта R, G, B на пиксель). Здесь считаются различные цвета
/// и составляются две задачи: «глубина по палитре» и «объём по размерам и глубине».
/// Картинка нигде не сохраняется — она живёт в памяти, пока идёт подсчёт.
/// </summary>
public static class ImageHandler
{
    public const long MaxPixels = 4_000_000;            // например, 2000×2000
    private const int ColorCountLimit = 65_536;
    private const long TrueColorPalette = 1L << 24;

    public static string? Validate(int width, int height)
    {
        if (width < 1 || height < 1)
            return "Не удалось узнать размеры картинки.";
        if ((long)width * height > MaxPixels)
            return "Картинка слишком большая: максимум 4 миллиона пикселей (например, 2000×2000).";
        return null;
    }

    public static ImageResponse Analyze(int width, int height, byte[] rgb)
    {
        var error = Validate(width, height);
        if (error != null)
            return ImageResponse.Fail(error);

        if (rgb.Length != width * height * 3)
            return ImageResponse.Fail("Данные картинки не совпали с её размерами.");

        // Считаем различные цвета. Дальше предела считать незачем: это «полноцветная» картинка.
        var colors = new HashSet<int>();
        bool exceeded = false;

        for (int i = 0; i + 2 < rgb.Length; i += 3)
        {
            colors.Add((rgb[i] << 16) | (rgb[i + 1] << 8) | rgb[i + 2]);
            if (colors.Count > ColorCountLimit)
            {
                exceeded = true;
                break;
            }
        }

        long palette;
        string note;

        if (exceeded)
        {
            palette = TrueColorPalette;
            note = "В картинке больше 65 536 различных цветов — считаем её полноцветной (палитра 2^24 цветов). " +
                   "Для интересных задач загрузите картинку с малым числом цветов (рисунок, логотип, пиксель-арт).";
        }
        else if (colors.Count < 2)
        {
            palette = 2;
            note = "Картинка одноцветная; минимальная палитра — 2 цвета (1 бит).";
        }
        else
        {
            palette = colors.Count;
            note = $"Найдено различных цветов: {colors.Count}. Прозрачность не учитывается.";
        }

        int depth = ImageMath.DepthForColors(palette);

        var depthSpec = new TaskSpec(TaskKind.Depth, width, height, palette, 0, Unit.Bits);

        long bits = ImageMath.TotalBits(width, height, depth);
        var volumeUnit = bits % 8 == 0 ? Unit.Bytes : Unit.Bits;     // ответ должен быть целым
        var volumeSpec = new TaskSpec(TaskKind.Volume, width, height, 0, depth, volumeUnit);

        return new ImageResponse(
            Error: null,
            Info: new ImageInfoResult(width, height, palette, note),
            DepthTask: new GeneratedTask(depthSpec, TaskText.Condition(depthSpec)),
            VolumeTask: new GeneratedTask(volumeSpec, TaskText.Condition(volumeSpec)));
    }
}
