"use strict";

/* Страница 1: визуализация (сетка пикселей, палитра) и задание «Предскажите». */

const FIELDS = ["v-width", "v-height", "v-colors"];
let selectedPixel = { col: 0, row: 0 };
let previous = null;      // прошлое описание — чтобы показать, что изменилось
let current = null;

async function refreshVisualizer() {
  const values = readInts(FIELDS);
  if (!values) {
    say($("v-error"), "Введите целые числа во все три поля.", "bad");
    return;
  }
  const [width, height, colors] = values;

  const d = await api("/api/describe", {
    width, height, colors, pixelCol: selectedPixel.col, pixelRow: selectedPixel.row,
  });
  if (d.error) {
    say($("v-error"), d.error, "bad");
    return;
  }
  say($("v-error"), "", "");
  selectedPixel = { col: d.selectedCol, row: d.selectedRow };
  previous = current;
  current = { width, height, colors, depth: d.depth, pixels: d.pixels, bits: d.totalBits };

  // палитра
  const palette = $("v-palette");
  palette.innerHTML = "";
  d.paletteCodes.forEach((code, index) => {
    const swatch = makeElement("div", "swatch");
    const box = makeElement("div");
    box.style.background = colorOf(index, colors);
    swatch.append(box, makeElement("span", "", code));
    palette.append(swatch);
  });
  if (colors > d.paletteCodes.length) {
    palette.append(makeElement("span", "small", "… и ещё " + (colors - d.paletteCodes.length) + " цветов"));
  }

  // сетка пикселей
  const grid = $("v-grid");
  grid.innerHTML = "";
  grid.style.gridTemplateColumns = "repeat(" + d.cols + ", 22px)";
  d.cells.forEach((colorIndex, i) => {
    const col = i % d.cols;
    const row = Math.floor(i / d.cols);
    const cell = makeElement("button", "pixel");
    cell.type = "button";
    if (col === d.selectedCol && row === d.selectedRow) cell.classList.add("selected");
    cell.style.background = colorOf(colorIndex, colors);
    cell.title = "столбец " + (col + 1) + ", строка " + (row + 1);
    cell.addEventListener("click", () => {
      selectedPixel = { col, row };
      refreshVisualizer();
    });
    grid.append(cell);
  });
  $("v-grid-note").textContent = (d.cols < width || d.rows < height)
    ? "Показаны первые " + d.cols + "×" + d.rows + " пикселей из " + width + "×" + height +
      " (на экране не больше 256 пикселей, изображение показано условно)."
    : "Показаны все " + width + "×" + height + " пикселей.";

  // выбранный пиксель
  $("v-selected-box").style.background = colorOf(d.selectedColorIndex, colors);
  $("v-selected-box").textContent = d.selectedCode;
  $("v-selected-text").textContent =
    "Пиксель: столбец " + (d.selectedCol + 1) + ", строка " + (d.selectedRow + 1) + ".\n" +
    "Цвет № " + d.selectedColorIndex + " из " + colors + ".\n" +
    "Код цвета: " + d.selectedCode + " (" + d.depth + " бит).";

  // расчёт: что изменилось — красным
  const info = $("v-info");
  info.innerHTML = "";
  const line = (text, was, now) => {
    const p = makeElement("p", "pre");
    p.style.margin = "2px 0";
    p.append(text);
    if (previous && was !== now) p.append(makeElement("span", "changed", "   (было " + was + ")"));
    info.append(p);
  };
  line("Глубина цвета i = " + d.depth + " бит   (2^" + d.depth + " = " + 2 ** d.depth + " ≥ " + colors + ")",
    previous && previous.depth, d.depth);
  line("Пикселей: " + width + " × " + height + " = " + d.pixels, previous && previous.pixels, d.pixels);
  line("Объём: " + d.pixels + " × " + d.depth + " = " + d.totalBits + " бит = " + d.bytes + " байт = " + d.kib + " КиБ",
    previous && previous.bits, d.totalBits);

  $("p-now").textContent = "Сейчас: " + width + "×" + height + " пикселей, " + colors +
    " цветов, глубина " + d.depth + " бит. Объём = " + d.totalBits + " бит.";
}

FIELDS.forEach((id) =>
  $(id).addEventListener("input", () => { selectedPixel = { col: 0, row: 0 }; refreshVisualizer(); }));

// ---------------------------------------------------------------- «Предскажите»

$("p-check").addEventListener("click", async () => {
  const values = readInts(FIELDS);
  if (!values) { say($("p-message"), "Сначала введите целые числа в поля «Примера 1».", "bad"); return; }

  const response = await api("/api/predict", {
    width: values[0], height: values[1], colors: values[2],
    change: $("p-change").value, answer: $("p-answer").value,
  });

  const result = response.result;
  if (result.status === "Correct") {
    say($("p-message"), result.message + " Изменение применено в рамке «Пример 1» — посмотрите, как поменялись цифры.", "ok");
    $("v-width").value = response.width;
    $("v-height").value = response.height;
    $("v-colors").value = response.colors;
    selectedPixel = { col: 0, row: 0 };
    $("p-answer").value = "";
    refreshVisualizer();
    unlockNextLink();
  } else {
    const retry = result.status === "Wrong" ? " Исправьте ответ и проверьте ещё раз." : "";
    say($("p-message"), result.message + retry, kindOfStatus(result.status));
  }
});
submitOnEnter($("p-answer"), $("p-check"));

refreshVisualizer();