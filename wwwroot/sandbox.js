"use strict";

/* Страница 6: случайные задачи, своя картинка, статистика. */

// ---------------------------------------------------------------- клиент и статистика

function getClientId() {
  try {
    let id = localStorage.getItem("clientId");
    if (!id) {
      id = (window.crypto && crypto.randomUUID)
        ? crypto.randomUUID()
        : Date.now() + "-" + Math.random().toString(16).slice(2);
      localStorage.setItem("clientId", id);
    }
    return id;
  } catch (e) {
    return "guest-" + Date.now();      // localStorage недоступен: статистика только на эту страницу
  }
}
const CLIENT_ID = getClientId();

function statRow(title, correct, total, percent) {
  const row = makeElement("div", "stat-row");
  row.append(makeElement("div", "", title + ": " + correct + " из " + total + " верно (" + percent + "%)"));
  const bar = makeElement("div", "bar");
  const fill = makeElement("div");
  fill.style.width = percent + "%";
  bar.append(fill);
  row.append(bar);
  return row;
}

function showStats(s) {
  const box = $("s-stats");
  box.innerHTML = "";
  if (s.total === 0) {
    box.textContent = "Решённых задач пока нет. Решите первую!";
    return;
  }
  box.append(statRow("За всё время", s.correct, s.total, s.totalPercent));
  box.append(statRow("За последние " + s.last10Count, s.last10Correct, s.last10Count, s.last10Percent));
}

async function loadStats() {
  showStats(await api("/api/stats?clientId=" + encodeURIComponent(CLIENT_ID)));
}

// ---------------------------------------------------------------- задачи

let currentTask = null;

function showTask(task) {
  currentTask = task;
  $("s-text").textContent = task.text;
  $("s-answer").value = "";
  $("s-answer").disabled = false;
  $("s-check").disabled = false;
  $("s-solution-box").hidden = true;
  say($("s-message"), "", "");
  $("s-answer").focus();
}

$("s-random").addEventListener("click", async () => showTask(await api("/api/generate")));
$("s-depth").addEventListener("click", async () => showTask(await api("/api/generate?kind=depth")));
$("s-volume").addEventListener("click", async () => showTask(await api("/api/generate?kind=volume")));
$("s-again").addEventListener("click", async () => showTask(await api("/api/generate")));

$("s-check").addEventListener("click", async () => {
  if (!currentTask) { say($("s-message"), "Сначала получите задачу: нажмите «Случайная задача».", "info"); return; }

  const response = await api("/api/check", {
    task: currentTask.spec, answer: $("s-answer").value, clientId: CLIENT_ID, sandbox: true,
  });
  const result = response.result;
  say($("s-message"), result.message, kindOfStatus(result.status));
  if (result.status === "InvalidInput") return;      // не число — можно исправить и ответить ещё раз

  // одна попытка на задачу: показываем решение и обновляем статистику
  $("s-check").disabled = true;
  $("s-answer").disabled = true;
  $("s-solution").textContent = (response.solution || []).join("\n");
  $("s-solution-box").hidden = false;
  if (response.stats) showStats(response.stats);
});
submitOnEnter($("s-answer"), $("s-check"));

// ---------------------------------------------------------------- своя картинка

const MAX_IMAGE_PIXELS = 4000000;
let imageTasks = null;

/** Браузер раскрывает файл в пиксели: RGB, по 3 байта на пиксель. */
function readPixels(file) {
  return new Promise((resolve, reject) => {
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onerror = () => reject(new Error("Браузер не смог открыть этот файл как картинку."));
    img.onload = () => {
      const width = img.naturalWidth;
      const height = img.naturalHeight;
      if (!width || !height) { reject(new Error("Не удалось узнать размеры картинки.")); return; }
      if (width * height > MAX_IMAGE_PIXELS) {
        reject(new Error("Картинка слишком большая: " + width + "×" + height + ". Максимум — 4 миллиона пикселей."));
        return;
      }

      const canvas = document.createElement("canvas");
      canvas.width = width;
      canvas.height = height;
      const ctx = canvas.getContext("2d", { willReadFrequently: true });
      ctx.drawImage(img, 0, 0);
      const rgba = ctx.getImageData(0, 0, width, height).data;

      const rgb = new Uint8Array(width * height * 3);
      for (let i = 0, j = 0; i < rgba.length; i += 4) {
        rgb[j++] = rgba[i];
        rgb[j++] = rgba[i + 1];
        rgb[j++] = rgba[i + 2];
      }
      resolve({ width, height, rgb, url });
    };
    img.src = url;
  });
}

$("s-file").addEventListener("change", async (event) => {
  const file = event.target.files[0];
  if (!file) return;

  $("s-img-depth").disabled = true;
  $("s-img-volume").disabled = true;
  imageTasks = null;
  say($("s-image-message"), "Читаю картинку…", "info");

  let picture;
  try {
    picture = await readPixels(file);
  } catch (e) {
    say($("s-image-message"), e.message, "bad");
    $("s-preview").hidden = true;
    return;
  }

  const response = await fetch("/api/image?width=" + picture.width + "&height=" + picture.height, {
    method: "POST",
    headers: { "Content-Type": "application/octet-stream" },
    body: picture.rgb,
  }).catch((e) => { showFatal(SERVER_HELP); throw e; });
  const data = await response.json();

  if (data.error) {
    say($("s-image-message"), data.error, "bad");
    $("s-preview").hidden = true;
    return;
  }

  imageTasks = data;
  say($("s-image-message"),
    "Размер: " + data.info.width + "×" + data.info.height + " пикселей.\n" + data.info.note +
    "\nТеперь нажмите одну из кнопок с задачей по картинке.", "ok");
  $("s-preview").src = picture.url;
  $("s-preview").hidden = false;
  $("s-img-depth").disabled = false;
  $("s-img-volume").disabled = false;
});

function showImageTask(task) {
  showTask(task);
  $("s-text").scrollIntoView({ behavior: "smooth", block: "center" });
}

$("s-img-depth").addEventListener("click", () => imageTasks && showImageTask(imageTasks.depthTask));
$("s-img-volume").addEventListener("click", () => imageTasks && showImageTask(imageTasks.volumeTask));

loadStats();
