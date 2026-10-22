"use strict";

/*
 * Общий код всех страниц. Страница только показывает данные и отправляет действия на сервер.
 * Все расчёты, проверки ответов, генерация задач и статистика — в C# (папка Backend).
 */

const $ = (id) => document.getElementById(id);
const PAGE = document.body.dataset.page;          // intro, main, task1, mistake, find-error, sandbox

// ---------------------------------------------------------------- запросы к серверу

const SERVER_HELP =
  "Нет связи с сервером. Страницы работают только вместе с серверной частью:\n" +
  "1) откройте папку проекта и запустите run.bat (или команду «dotnet run»);\n" +
  "2) откройте в браузере адрес http://localhost:5080 (а не файл из папки).";

function showFatal(text) {
  const box = $("fatal");
  if (!box) return;
  box.textContent = text;
  box.className = "message bad";
  box.hidden = false;
}

/** Запрос к серверу: без тела — GET, с телом — POST (JSON). */
async function api(path, body) {
  const options = body === undefined
    ? {}
    : { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) };

  let response;
  try {
    response = await fetch(path, options);
  } catch (e) {
    showFatal(SERVER_HELP);
    throw e;
  }
  if (!response.ok) {
    showFatal("Сервер ответил ошибкой " + response.status + ". Обновите страницу.");
    throw new Error("HTTP " + response.status);
  }
  return response.json();
}

if (location.protocol === "file:") showFatal(SERVER_HELP);

// ---------------------------------------------------------------- мелкие помощники

/** Показывает сообщение в блоке. kind: "ok" | "bad" | "info". Пустой текст прячет блок. */
function say(node, text, kind) {
  node.textContent = text;
  node.className = "message" + (kind ? " " + kind : "");
  node.hidden = !text;
}

function kindOfStatus(status) {
  return status === "Correct" ? "ok" : status === "Wrong" ? "bad" : "info";
}

/** Читает целые числа из полей. Если хоть одно не целое (или слишком огромное) — null. */
function readInts(ids) {
  const values = ids.map((id) => ($(id).value.trim() === "" ? NaN : Number($(id).value)));
  return values.every((v) => Number.isInteger(v) && Math.abs(v) <= 2147483647) ? values : null;
}

/**
 * Цвет клетки по номеру в палитре (только оформление).
 * Соседние номера заметно отличаются при любом размере палитры.
 */
function colorOf(index, colors) {
  const hue = (index * 137.5) % 360;
  const lightness = 38 + ((index * 17) % 30);
  return "hsl(" + Math.round(hue) + ", 75%, " + lightness + "%)";
}

function makeElement(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

/** Enter в поле ввода = нажатие кнопки. */
function submitOnEnter(input, button) {
  input.addEventListener("keydown", (e) => { if (e.key === "Enter") button.click(); });
}

// ---------------------------------------------------------------- прогресс по страницам

function isDone(page) {
  try { return localStorage.getItem("done:" + page) === "1"; } catch (e) { return false; }
}

function markDone(page) {
  try { localStorage.setItem("done:" + page, "1"); } catch (e) { /* без запоминания */ }
}

// Подсвечиваем текущую страницу и пройденные
document.querySelectorAll("nav a").forEach((link) => {
  if (link.dataset.page === PAGE) link.classList.add("current");
  if (isDone(link.dataset.page)) link.classList.add("done");
});

/**
 * Кнопка-ссылка «Дальше». Пока страница не пройдена, она заблокирована.
 * Разблокировать: unlockNextLink().
 */
const nextLink = $("next-link");

function lockNextLink() {
  if (!nextLink) return;
  nextLink.classList.add("locked");
  nextLink.setAttribute("aria-disabled", "true");
  if ($("next-hint")) $("next-hint").hidden = false;
}

function unlockNextLink() {
  markDone(PAGE);
  document.querySelectorAll("nav a").forEach((link) => {
    if (link.dataset.page === PAGE) link.classList.add("done");
  });
  if (!nextLink) return;
  nextLink.classList.remove("locked");
  nextLink.removeAttribute("aria-disabled");
  if ($("next-hint")) $("next-hint").hidden = true;
}

if (nextLink) {
  nextLink.addEventListener("click", (e) => { if (nextLink.classList.contains("locked")) e.preventDefault(); });
  if (isDone(PAGE)) unlockNextLink(); else lockNextLink();
}