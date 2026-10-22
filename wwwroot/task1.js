"use strict";

/* Страница 3: задача с вводом ответа, объяснением ошибки и повторной попыткой. */

let wrongAttempts = 0;

async function start() {
  const lessons = await api("/api/lessons");
  $("t1-text").textContent = lessons.task1.text;

  $("t1-check").addEventListener("click", async () => {
    const response = await api("/api/check", {
      task: lessons.task1.spec, answer: $("t1-answer").value, clientId: null, sandbox: false,
    });
    const result = response.result;

    if (result.status === "Correct") {
      say($("t1-message"), result.message, "ok");
      $("t1-check").disabled = true;
      $("t1-answer").disabled = true;
      unlockNextLink();
      return;
    }

    if (result.status === "Wrong") {
      wrongAttempts++;
      say($("t1-message"), result.message + "\nИсправьте ответ и нажмите «Проверить» ещё раз.", "bad");
      if (wrongAttempts >= 3) $("t1-help").hidden = false;      // после трёх ошибок можно посмотреть решение
    } else {
      say($("t1-message"), result.message, "info");
    }
  });
  submitOnEnter($("t1-answer"), $("t1-check"));

  $("t1-show").addEventListener("click", async () => {
    const lines = await api("/api/lesson-solution");
    $("t1-solution").textContent = lines.join("\n");
    $("t1-solution-box").hidden = false;
    say($("t1-message"), "Разберите решение и вернитесь к следующей странице. Кнопка «Дальше» открыта.", "info");
    unlockNextLink();
  });
}

start();
