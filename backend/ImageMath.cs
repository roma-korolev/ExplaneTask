using System.Globalization;

namespace ObyasniZadachu.Backend;

/// <summary>
/// Все расчёты проекта. Здесь нет текстов для экрана и нет работы с HTTP.
/// Арифметика целочисленная (long) и decimal — никаких double, поэтому ответы точные.
/// </summary>
public static class ImageMath
{
    public const int MaxSide = 20_000;
    public const long MaxPixels = 100_000_000;
    public const long MaxColors = 1L << 32;
    public const int MaxDepth = 32;

    /// <summary>Наименьшее i, при котором 2^i >= colors (округление вверх без логарифмов).</summary>
    public static int DepthForColors(long colors)
    {
        if (colors < 2)
            throw new ArgumentOutOfRangeException(nameof(colors), "Палитра должна содержать минимум 2 цвета.");

        int bits = 0;
        while ((1L << bits) < colors) bits++;
        return bits;
    }

    public static bool IsPowerOfTwo(long n) => n > 0 && (n & (n - 1)) == 0;

    public static long Pixels(int width, int height) => checked((long)width * height);

    public static long TotalBits(int width, int height, int depth) => checked(Pixels(width, height) * depth);

    /// <summary>Перевод битов в выбранную единицу: 1 байт = 8 бит, 1 КиБ = 1024 байта.</summary>
    public static decimal InUnit(long bits, Unit unit) => unit switch
    {
        Unit.Bits => bits,
        Unit.Bytes => bits / 8m,
        Unit.KiB => bits / 8192m,
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    /// <summary>Глубина цвета задачи: данная в условии или выведенная из палитры.</summary>
    public static int DepthOf(TaskSpec task) => task.Depth > 0 ? task.Depth : DepthForColors(task.Colors);

    /// <summary>Правильный ответ задачи.</summary>
    public static decimal Expected(TaskSpec task) => task.Kind == TaskKind.Depth
        ? DepthForColors(task.Colors)
        : InUnit(TotalBits(task.Width, task.Height, DepthOf(task)), task.Unit);

    /// <summary>
    /// Сравнение ответа с правильным. Точное совпадение; для дробных ответов (КиБ) также принимаем
    /// запись с 6 знаками после запятой (как на экране) и округление до сотых.
    /// </summary>
    public static bool Matches(decimal value, decimal expected)
    {
        if (value == expected) return true;
        if (expected == decimal.Truncate(expected)) return false;
        if (value == Math.Round(expected, 6)) return true;

        var rounded = Math.Round(expected, 2);
        return rounded != 0 && value == rounded;
    }

    // ---------- Проверка входных данных (выполняется на сервере) ----------

    public static string? ValidateSize(int width, int height)
    {
        if (width < 1 || height < 1 || width > MaxSide || height > MaxSide)
            return $"Ширина и высота — целые числа от 1 до {MaxSide}.";
        if ((long)width * height > MaxPixels)
            return $"Слишком много пикселей: максимум {MaxPixels}.";
        return null;
    }

    public static string? ValidateColors(long colors) =>
        colors < 2 || colors > MaxColors ? $"Число цветов — целое от 2 до {MaxColors}." : null;

    public static string? Validate(TaskSpec task)
    {
        if (!Enum.IsDefined(task.Kind) || !Enum.IsDefined(task.Unit))
            return "Неизвестный тип задачи или единица измерения.";

        if (task.Kind == TaskKind.Depth)
            return ValidateColors(task.Colors);

        var sizeError = ValidateSize(task.Width, task.Height);
        if (sizeError != null) return sizeError;

        if (task.Depth != 0)
            return task.Depth is < 1 or > MaxDepth ? $"Глубина цвета — от 1 до {MaxDepth} бит." : null;

        return ValidateColors(task.Colors);
    }

    // ---------- Числа: разбор ввода и вывод ----------

    /// <summary>Понимает «2048», «0,25», «0.25», «2 048». Всё остальное — не число.</summary>
    public static bool TryParseAnswer(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 30) return false;

        var clean = text.Trim().Replace(" ", "").Replace("\u00A0", "").Replace(',', '.');
        return decimal.TryParse(clean, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Число для экрана: 0,25 вместо 0.250000.</summary>
    public static string Format(decimal value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture).Replace('.', ',');

    /// <summary>Двоичная запись числа ровно в «bits» разрядов: ToBinary(5, 4) = "0101".</summary>
    public static string ToBinary(long value, int bits) => Convert.ToString(value, 2).PadLeft(bits, '0');

    /// <summary>
    /// Условная «картинка» для визуализации: номер цвета пикселя по его порядковому номеру.
    /// Настоящих пикселей нет — нужен лишь набросок, где цвета чередуются.
    /// </summary>
    public static long ColorIndexOfPixel(long pixelIndex, long colors) => (pixelIndex * 7 + 3) % colors;
}
