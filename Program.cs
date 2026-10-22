using System.Text.Json.Serialization;
using ObyasniZadachu.Backend;

var builder = WebApplication.CreateBuilder(args);

// Адрес задан жёстко, чтобы всегда знать, что открывать в браузере.
builder.WebHost.UseUrls("http://localhost:5080");

// Перечисления (Unit, TaskKind, CheckStatus) ходят между страницей и сервером как строки.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton<StatisticsService>();

var app = builder.Build();

app.UseDefaultFiles();   // открывает wwwroot/index.html
app.UseStaticFiles();    // раздаёт остальные страницы: main.html, task1.html, ...

// ---------- Обработчик кнопок ----------

// Визуализация: сетка пикселей, палитра, выбранный пиксель
app.MapPost("/api/describe", (DescribeRequest request) => ButtonHandler.Describe(request));

// Кнопки «Следующий шаг» / «Сначала» в пошаговых разборах
app.MapPost("/api/button", (ButtonRequest request) => ButtonHandler.Handle(request));

// Ответ на вопрос внутри шага (разблокирует «Следующий шаг»)
app.MapPost("/api/step-answer", (StepAnswerRequest request) => ButtonHandler.CheckStep(request));

// «Предскажите»: изменить один параметр и посчитать новый объём
app.MapPost("/api/predict", (PredictRequest request) => ButtonHandler.Predict(request));

// ---------- Чекер ----------

// Проверка ответа. В песочнице ещё и пишет статистику.
app.MapPost("/api/check", (CheckRequest request, StatisticsService statistics) =>
{
    if (request.Task is null)
        return Results.BadRequest("Нет условия задачи.");

    var result = Checker.Check(request.Task, request.Answer);

    StatsSnapshot? snapshot = null;
    string[]? solution = null;

    // Неверный ввод (не число) не считается попыткой: его можно исправить.
    if (request.Sandbox && result.Status != CheckStatus.InvalidInput)
    {
        solution = TaskText.Solution(request.Task);
        if (StatisticsService.IsValidClientId(request.ClientId))
            snapshot = statistics.Record(request.ClientId!, result.Status == CheckStatus.Correct);
    }

    return Results.Ok(new CheckResponse(result, snapshot, solution));
});

// Задача «найди шаг с ошибкой»
app.MapPost("/api/check-mistake", (MistakeAnswer answer) => Checker.CheckMistake(answer));

// ---------- Готовые уроки ----------

app.MapGet("/api/lessons", () => Lessons.Overview());
app.MapGet("/api/lesson-solution", () => TaskText.Solution(Lessons.Task1));

// ---------- Генератор задач ----------

// kind = depth | volume | пусто (случайный тип)
app.MapGet("/api/generate", (string? kind) => TaskGenerator.Generate(kind));

// ---------- Обработчик картинок ----------
// Браузер раскрывает файл в пиксели (3 байта RGB на пиксель) и присылает их сюда.
// Сервер считает различные цвета и составляет две задачи.
app.MapPost("/api/image", async (int width, int height, HttpRequest request) =>
{
    var error = ImageHandler.Validate(width, height);
    if (error != null)
        return Results.Ok(ImageResponse.Fail(error));

    var data = new byte[width * height * 3];
    try
    {
        await request.Body.ReadExactlyAsync(data);
    }
    catch (EndOfStreamException)
    {
        return Results.Ok(ImageResponse.Fail("Данные картинки получены не полностью. Попробуйте ещё раз."));
    }

    return Results.Ok(ImageHandler.Analyze(width, height, data));
});

// ---------- Статистика ----------

app.MapGet("/api/stats", (string? clientId, StatisticsService statistics) =>
    StatisticsService.IsValidClientId(clientId)
        ? Results.Ok(statistics.Get(clientId!))
        : Results.BadRequest("Неверный идентификатор."));

app.Run();
