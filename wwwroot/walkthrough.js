"use strict";

/*
 * Пошаговый разбор (страницы «Основной пример» и «Разбор ошибки»).
 * Номер шага хранится здесь, а что показать на шаге и верен ли ответ — решает сервер.
 * Кнопка «Следующий шаг» открывается только после верного ответа на вопрос шага.
 */

const SECTION = document.body.dataset.section;       // "main" или "mistake"
const HAS_INPUTS = !!$("m-width");                    // только в основном примере есть поля

let step = 0;
let stepCount = 0;
let solved = false;
let numbers = { width: 0, height: 0, colors: 0 };

function readNumbers() {
  if (!HAS_INPUTS) return { width: 0, height: 0, colors: 0 };
  const values = readInts(["m-width", "m-height", "m-colors"]);
  return values ? { width: values[0], height: values[1], colors: values[2] } : null;
}

async function load(action) {
  const nums = readNumbers();
  if (!nums) {
    say($("error"), "Введите целые числа во все три поля.", "bad");
    return;
  }

  const view = await api("/api/button", { section: SECTION, action, step, ...nums });
  if (view.error) {
    say($("error"), view.error, "bad");
    $("box").innerHTML = "";
    $("next").disabled = true;
    return;
  }

  say($("error"), "", "");
  numbers = nums;
  step = view.step;
  stepCount = view.stepCount;
  solved = false;
  render(view);
}

function render(view) {
  const box = $("box");
  box.innerHTML = "";

  // полоса прогресса
  const progress = makeElement("div", "progress");
  for (let i = 0; i < view.stepCount; i++) {
    progress.append(makeElement("span", i < view.step ? "done" : i === view.step ? "now" : ""));
  }
  box.append(progress);
  box.append(makeElement("p", "small", "Шаг " + (view.step + 1) + " из " + view.stepCount));
  box.append(makeElement("p", "step-context", view.context));
  box.append(makeElement("h3", "", view.title));
  box.append(makeElement("p", "step-text", view.text));

  const solution = makeElement("div", "solution");
  solution.hidden = true;

  if (view.question) box.append(buildQuestion(view.question, solution));
  box.append(solution);

  $("next").disabled = true;
  $("next-step-hint").textContent = "Ответьте на вопрос и нажмите «Проверить» — тогда откроется следующий шаг.";
}

function buildQuestion(question, solution) {
  const block = makeElement("div", "question");
  block.append(makeElement("p", "prompt", "Вопрос: " + question.prompt));

  const hint = makeElement("details");
  hint.append(makeElement("summary", "", "Подсказка"), makeElement("span", "", question.hint));
  block.append(hint);

  let readAnswer;
  let focusNode = null;

  if (question.kind === "choice") {
    const choice = makeElement("div", "choice");
    question.options.forEach((text, i) => {
      const label = makeElement("label");
      const radio = makeElement("input");
      radio.type = "radio";
      radio.name = "answer";
      radio.value = String(i);
      label.append(radio, " " + text);
      choice.append(label);
    });
    block.append(choice);
    readAnswer = () => {
      const picked = block.querySelector("input[name='answer']:checked");
      return picked ? picked.value : "";
    };
  } else {
    const input = makeElement("input");
    input.type = "text";
    input.inputMode = "decimal";
    input.placeholder = "ответ — число";
    block.append(input);
    readAnswer = () => input.value;
    focusNode = input;
  }

  const button = makeElement("button", "", "Проверить");
  button.type = "button";
  const message = makeElement("p", "message");
  message.hidden = true;
  block.append(" ", button, message);

  button.addEventListener("click", async () => {
    if (question.kind === "choice" && readAnswer() === "") {
      say(message, "Сначала выберите один из вариантов.", "info");
      return;
    }

    const response = await api("/api/step-answer", {
      section: SECTION, step, ...numbers, answer: readAnswer(),
    });
    say(message, response.message + (response.status === "Wrong" ? " Попробуйте ещё раз." : ""), kindOfStatus(response.status));
    if (response.status !== "Correct") return;

    // верный ответ: показываем разбор шага и открываем «Следующий шаг»
    solved = true;
    button.disabled = true;
    block.querySelectorAll("input").forEach((node) => { node.disabled = true; });
    solution.textContent = response.solution;
    solution.hidden = false;

    if (step >= stepCount - 1) {
      $("next").disabled = true;
      $("next-step-hint").textContent = "Это был последний шаг. Кнопка «Дальше» внизу страницы открыта.";
      unlockNextLink();
    } else {
      $("next").disabled = false;
      $("next-step-hint").textContent = "Верно! Нажмите «Следующий шаг».";
    }
  });

  if (focusNode) submitOnEnter(focusNode, button);
  return block;
}

$("next").addEventListener("click", () => { if (solved) load("next"); });
$("reset").addEventListener("click", () => { step = 0; load("reset"); });

// смена исходных данных начинает разбор сначала (с небольшой задержкой, пока человек печатает)
if (HAS_INPUTS) {
  let timer = null;
  ["m-width", "m-height", "m-colors"].forEach((id) =>
    $(id).addEventListener("input", () => {
      clearTimeout(timer);
      timer = setTimeout(() => { step = 0; load("reset"); }, 400);
    }));
}

load("reset");
