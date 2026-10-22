"use strict";

/* Страница 5: найти шаг с ошибкой и дать правильный ответ. */

async function start() {
  const lessons = await api("/api/lessons");
  $("f-condition").textContent = lessons.findError.condition;

  const list = $("f-steps");
  lessons.findError.steps.forEach((text, i) => {
    const label = makeElement("label");
    const radio = makeElement("input");
    radio.type = "radio";
    radio.name = "f-step";
    radio.value = String(i + 1);
    label.append(radio, " Шаг " + (i + 1) + ". " + text);
    list.append(label);
  });

  $("f-check").addEventListener("click", async () => {
    const picked = document.querySelector("input[name='f-step']:checked");
    const result = await api("/api/check-mistake", {
      step: picked ? Number(picked.value) : 0, answer: $("f-answer").value,
    });

    if (result.status === "Correct") {
      say($("f-message"), result.message, "ok");
      $("f-check").disabled = true;
      $("f-answer").disabled = true;
      document.querySelectorAll("input[name='f-step']").forEach((node) => { node.disabled = true; });
      unlockNextLink();
    } else {
      const retry = result.status === "Wrong" ? "\nПопробуйте ещё раз." : "";
      say($("f-message"), result.message + retry, kindOfStatus(result.status));
    }
  });
  submitOnEnter($("f-answer"), $("f-check"));
}

start();
