#!/usr/bin/env node
// boaz · documentos de requisito em Markdown (0_Assets/requisitos/tema.md)
//
//   node 0_Assets/requisitos/requisito.mjs html <doc.md> [saida.html] [--sonda]
//       gera o HTML do documento com a casca (casca.html): temas, paleta das vistas, PDF e marca d'água.
//       Saída fora da pasta do documento ganha <base> para as imagens relativas continuarem valendo.
//       --sonda: acrescenta <pre id="boaz-sonda"> com as imagens que não carregaram (para o Chrome headless).
//   node 0_Assets/requisitos/requisito.mjs conferir <doc.md>...
//       confere marca d'água, links, imagens e âncoras (erro: sai com 1) e linhas de Gherkin > 100 (aviso;
//       --avisos lista cada um).
//
// Sem dependências: entende só o subconjunto de Markdown do tema.md.

import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { dirname, resolve, relative, basename } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const AQUI = dirname(fileURLToPath(import.meta.url));
const RAIZ = resolve(AQUI, "../..");
const CAMPOS = ["projeto", "documento", "tipo", "titulo", "versao", "data", "status", "objetivo"];

// ── front matter ────────────────────────────────────────────
function lerDocumento(arquivo) {
  const texto = readFileSync(arquivo, "utf8").replace(/\r\n/g, "\n");
  const m = texto.match(/^---\n([\s\S]*?)\n---\n/);
  if (!m) throw new Error(`${arquivo}: sem front matter (bloco --- no início)`);
  const meta = {};
  for (const linha of m[1].split("\n")) {
    const kv = linha.match(/^([a-z_]+):\s*(.*)$/);
    if (!kv) continue;
    let v = kv[2].trim();
    if (v.startsWith('"')) v = JSON.parse(v);
    meta[kv[1]] = v;
  }
  return { meta, corpo: texto.slice(m[0].length) };
}

// ── inline ──────────────────────────────────────────────────
const esc = (s) => s.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
const ehId = (s) => /^(RF|RNF|RN|MOD)-\d+(-api)?$/.test(s);

function inline(md) {
  // links e imagens primeiro: o texto do link pode ter `código`
  const guardados = [];
  const guardar = (h) => `\u0001${guardados.push(h) - 1}\u0001`;
  md = md.replace(/\[!\[([^\]]*)\]\(([^)\s]+)\)\]\(([^)\s]+)\)/g, (_, alt, src, href) => guardar(`<a href="${href}"><img src="${src}" alt="${esc(alt)}"></a>`));
  md = md.replace(/!\[([^\]]*)\]\(([^)\s]+)\)/g, (_, alt, src) => guardar(`<img src="${src}" alt="${esc(alt)}">`));
  md = md.replace(/\[((?:[^\[\]`]|`[^`]*`|\\[\[\]])+)\]\(([^)\s]+)\)/g, (_, t, h) => guardar(`<a${/\.md(#|$)|^#/.test(h) ? ' class="ref"' : ""} href="${h}">${inline(t)}</a>`));
  return inlineSimples(md).replace(/\u0001(\d+)\u0001/g, (_, n) => guardados[n]);
}

function inlineSimples(md) {
  const partes = [];
  // separa os code spans (`x` ou `` x ``) do resto
  const re = /(``?)(.+?)\1/g;
  let i = 0, m;
  while ((m = re.exec(md))) {
    partes.push(texto(md.slice(i, m.index)));
    const c = m[1] === "``" ? m[2].trim() : m[2];
    partes.push(ehId(c) ? `<code class="id">${esc(c)}</code>` : `<code>${esc(c)}</code>`);
    i = re.lastIndex;
  }
  partes.push(texto(md.slice(i)));
  return partes.join("");
}

function texto(s) {
  const ancoras = [];
  s = s.replace(/<a id="([^"]+)"><\/a>/g, (_, id) => `\u0000${ancoras.push(id) - 1}\u0000`);
  s = s.replace(/&(?!(lt|gt|amp|quot|#\d+);)/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
  s = s.replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>");
  s = s.replace(/(^|[^\\*])\*([^*\s][^*]*?)\*/g, "$1<em>$2</em>");
  s = s.replace(/“([^”]+)”/g, '<q class="ui">$1</q>');
  s = s.replace(/\\([*`\[\]|\\])/g, "$1");
  return s.replace(/\u0000(\d+)\u0000/g, (_, n) => `<a id="${ancoras[n]}"></a>`);
}

// ── paletas das vistas (cores.json do design system) ────────
function paletas() {
  const arq = resolve(RAIZ, "0_Assets/design/tokens/cores.json");
  if (!existsSync(arq)) return "";
  const cores = JSON.parse(readFileSync(arq, "utf8"));
  const ids = Object.keys(cores.temas);
  ids.sort((a, b) => (a === cores.padrao ? -1 : b === cores.padrao ? 1 : 0));
  return ids.map((id) => {
    const t = cores.temas[id], c = t.md.sys.color.light;
    const nome = (t.$descricao || id).split(":")[0].trim();
    return [id, nome, ...[c.primary, c.secondary, c.tertiary].map((x) => x.$value.toLowerCase())].join("|");
  }).join(",");
}

// a vista traz as paletas quando o .svg tem os fragmentos #paleta-<id>-<modo>
const temPaletas = (svg) => existsSync(svg) && /id="paleta-[a-z0-9-]+-(claro|escuro)"/.test(readFileSync(svg, "utf8"));
let dirAtual = ".";

// ── blocos ──────────────────────────────────────────────────
function tabela(linhas) {
  const celulas = (l) => l.replace(/^\||\|$/g, "").split(/(?<!\\)\|/).map((c) => c.trim());
  const cab = celulas(linhas[0]);
  const corpo = linhas.slice(2).map(celulas);
  return { cab, corpo };
}

function renderCorpo(meta, corpo) {
  const linhas = corpo.split("\n");
  // o preâmbulo (carimbo, título, objetivo, sumário) é só para leitura no GitHub: o HTML o gera do front matter
  let i = linhas.findIndex((l) => l.trim() === "---");
  const preambulo = linhas.slice(0, i < 0 ? 0 : i).join("\n");
  const sumario = /\*\*Sumário:\*\*/.test(preambulo);
  const PALETAS = paletas();
  const out = [];
  let secao = null, artigo = false, ancora = null, historico = null, marcas = false, cartoes = null;

  const fecharMarcas = () => { if (marcas) { out.push("    </div>"); marcas = false; } };
  const fecharCartoes = () => { if (cartoes) { if (cartoes.aberto) out.push("      </article>"); out.push("    </div>"); cartoes = null; } };
  const fecharArtigo = () => { if (artigo) { out.push("    </article>"); artigo = false; } };
  const fecharSecao = () => { fecharArtigo(); fecharMarcas(); fecharCartoes(); if (secao) { out.push("  </section>"); secao = null; } };

  for (i = i < 0 ? 0 : i; i < linhas.length; i++) {
    const l = linhas[i];
    if (!l.trim() || l.trim() === "---" || /^<sub>/.test(l)) continue;
    const a = l.match(/^<a id="([^"]+)"><\/a>$/);
    if (a) { ancora = a[1]; continue; }

    let h = l.match(/^## (.+)$/);
    if (h) {
      fecharSecao();
      const id = ancora || h[1].toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]+/g, "-");
      ancora = null;
      if (id === "historico") { historico = []; continue; }
      secao = id;
      out.push(`  <section id="${id}">`, `    <h2>${inline(h[1])}</h2>`);
      continue;
    }

    h = l.match(/^### (.+)$/);
    if (h) {
      fecharArtigo(); fecharMarcas();
      if ((secao === "personas" || secao === "objetivos-e-metricas") && !ancora) {
        if (!cartoes) { cartoes = { tipo: secao === "personas" ? "persona" : "metrica" }; out.push(`    <div class="${cartoes.tipo}s">`); }
        if (cartoes.aberto) out.push("      </article>");
        const ini = cartoes.tipo === "persona" ? ` data-inicial="${esc(h[1].trim()[0].toUpperCase())}"` : "";
        out.push(`      <article class="${cartoes.tipo}"${ini}>`, `        <h3>${inline(h[1])}</h3>`);
        cartoes.aberto = true;
        continue;
      }
      fecharCartoes();
      if (ancora) {
        out.push(`    <article class="criterio" id="${ancora}">`, `      <h3>${inline(h[1])}</h3>`);
        artigo = true; ancora = null;
      } else {
        out.push(`    <h3>${inline(h[1])}</h3>`);
      }
      continue;
    }

    const cerca = l.match(/^(`{3,})(\w*)\s*$/);
    if (cerca) {
      const fim = linhas.findIndex((x, k) => k > i && x.trim() === cerca[1]);
      const bloco = linhas.slice(i + 1, fim).join("\n").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
      out.push(cerca[2] === "gherkin" ? `      <pre class="gherkin">${bloco}</pre>` : `      <pre><code>${bloco}</code></pre>`);
      i = fim;
      continue;
    }

    if (l.startsWith("|")) {
      const bloco = [];
      while (i < linhas.length && linhas[i].startsWith("|")) bloco.push(linhas[i++]);
      i--;
      const t = tabela(bloco);
      if (historico) { historico.push(...t.corpo); continue; }
      if (secao === "termos") {
        out.push('    <dl class="glossario">');
        for (const [termo, def] of t.corpo) {
          const id = (termo.match(/<a id="([^"]+)">/) || [])[1];
          const nome = termo.replace(/<a id="[^"]+"><\/a>/, "").replace(/^\*\*|\*\*$/g, "");
          out.push(`      <dt${id ? ` id="${id}"` : ""}>${inline(nome)}</dt><dd>${inline(def)}</dd>`);
        }
        out.push("    </dl>");
        continue;
      }
      out.push("    <table>", `      <thead><tr>${t.cab.map((c) => `<th>${inline(c)}</th>`).join("")}</tr></thead>`, "      <tbody>");
      for (const r of t.corpo) out.push(`        <tr>${r.map((c) => `<td>${inline(c)}</td>`).join("")}</tr>`);
      out.push("      </tbody>", "    </table>");
      continue;
    }

    // figura: legenda "**Título** — `caminho`" seguida da imagem
    const leg = l.match(/^\*\*(.+?)\*\* — `([^`]+)`(?: · fundo (claro|medio|escuro))?$/);
    const prox = linhas.slice(i + 1).find((x) => x.trim());
    if (leg && prox && /^\[?!\[/.test(prox)) {
      const k = linhas.indexOf(prox, i + 1);
      const img = prox.match(/!\[([^\]]*)\]\(([^)\s]+)\)/);
      const href = (prox.match(/\]\(([^)\s]+)\)$/) || [])[1] || img[2];
      if (prox.startsWith("[")) {
        fecharMarcas();
        const pal = PALETAS && temPaletas(resolve(dirAtual, img[2].split("#")[0])) ? ` data-paletas="${PALETAS}"` : "";
        out.push(`    <figure class="diagrama"${pal}>`,
          `      <a href="${href}"><img src="${img[2]}" alt="${esc(img[1])}"></a>`,
          `      <figcaption><strong>${inline(leg[1])}</strong><code>${esc(leg[2])}</code></figcaption>`, "    </figure>");
      } else {
        if (!marcas) { out.push('    <div class="marcas">'); marcas = true; }
        out.push(`      <figure class="marca${leg[3] ? ` fundo-${leg[3]}` : ""}">`,
          `        <div><img src="${img[2]}" alt="${esc(img[1])}"></div>`,
          `        <figcaption>${inline(leg[1])}<code>${esc(leg[2])}</code></figcaption>`, "      </figure>");
      }
      i = k;
      continue;
    }
    fecharMarcas();

    const item = l.match(/^(-|\d+\.) (.+)$/);
    if (item) {
      const ol = item[1] !== "-";
      const itens = [];
      while (i < linhas.length && /^(-|\d+\.) /.test(linhas[i])) itens.push(linhas[i++].replace(/^(-|\d+\.) /, ""));
      i--;
      if (cartoes) {
        for (const it of itens) {
          const r = it.match(/^\*\*(.+?):\*\* (.*)$/) || [null, "", it];
          if (cartoes.tipo === "persona") out.push(`        <p><span>${inline(r[1])}</span>${inline(r[2])}</p>`);
          else out.push(/^meta$/i.test(r[1]) ? `        <p class="meta">${inline(r[2])}</p>` : `        <p>${inline(r[2])}</p>`);
        }
        continue;
      }
      const cls = { "em-aberto": "pendencias", escopo: "escopo", "fora-de-escopo": "fora" }[secao];
      out.push(`    <${ol ? "ol" : "ul"}${cls ? ` class="${cls}"` : ""}>`);
      for (let it of itens) {
        it = inline(it);
        if (cls === "pendencias") it = it.replace(/ — <strong>([^<]+)<\/strong>$/, ' <span class="tag dono">$1</span>');
        out.push(`      <li>${it}</li>`);
      }
      out.push(`    </${ol ? "ol" : "ul"}>`);
      continue;
    }

    // parágrafo
    const par = [l.trim()];
    while (i + 1 < linhas.length && linhas[i + 1].trim() && !/^(#|<a id|```|\||- |\d+\. |---)/.test(linhas[i + 1])) par.push(linhas[++i].trim());
    out.push(`    <p>${inline(par.join(" "))}</p>`);
  }
  fecharSecao();
  return { main: out.join("\n"), historico: historico || [], sumario };
}

function statusClasse(s) {
  return "status-" + s.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, "").replace(/[^a-z0-9]+/g, "-");
}

function html(arquivo, saida, sonda) {
  const { meta, corpo } = lerDocumento(arquivo);
  const caminho = relative(RAIZ, resolve(arquivo));
  dirAtual = dirname(resolve(arquivo));
  const { main, historico, sumario } = renderCorpo(meta, corpo);
  const casca = readFileSync(resolve(AQUI, "casca.html"), "utf8").trimEnd();
  const dataCarimbo = meta.data.replace(" ", " · ");
  const base = saida && resolve(dirname(saida)) !== resolve(dirname(arquivo))
    ? `<base href="${pathToFileURL(resolve(dirname(arquivo))).href}/">\n` : "";
  const hist = historico.map((r, k) => {
    const v = r[0].replace(/^\*\*|\*\*$/g, "");
    return `      <tr><td>${k === 0 ? `<span class="boaz-versao">${esc(v)}</span>` : esc(v)}</td><td>${esc(r[1])}</td><td>${inline(r[2])}</td></tr>`;
  }).join("\n");
  const sondaJs = sonda ? `<pre id="boaz-sonda"></pre>
<script>
window.addEventListener("load", function () {
  setTimeout(function () {
    var r = [].map.call(document.images, function (im) { return { src: im.getAttribute("src"), ok: im.complete && im.naturalWidth > 0, w: im.naturalWidth }; });
    document.getElementById("boaz-sonda").textContent = JSON.stringify(r);
  }, 500);
});
</script>\n` : "";
  const doc = `<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
${base}<title>${esc(meta.titulo)} · ${esc(meta.projeto)} · ${esc(meta.versao)}</title>
<!-- boaz:marca — gerado de ${caminho} por 0_Assets/requisitos/requisito.mjs -->
<meta name="boaz:projeto" content="${esc(meta.projeto)}">
<meta name="boaz:documento" content="${esc(meta.documento)}">
<meta name="boaz:caminho" content="${esc(caminho)}">
<meta name="boaz:tipo" content="${esc(meta.tipo)}">
<meta name="boaz:versao" content="${esc(meta.versao)}">
<meta name="boaz:data" content="${esc(meta.data)}">
<meta name="boaz:status" content="${esc(meta.status)}">
${casca}
</head>
<body>

<header class="boaz-marca-topo">
  <p class="boaz-eyebrow">${esc(meta.projeto)} · ${esc(meta.tipo)}</p>
  <h1>${esc(meta.titulo)}</h1>
  <p class="boaz-objetivo"><span>Objetivo</span>${inline(meta.objetivo)}</p>
  <dl class="boaz-carimbo">
    <div><dt>Documento</dt><dd>${esc(meta.documento)}</dd></div>
    <div><dt>Versão</dt><dd><span class="boaz-versao">${esc(meta.versao)}</span></dd></div>
    <div><dt>Data</dt><dd class="boaz-data">${esc(dataCarimbo)}</dd></div>
    <div><dt>Status</dt><dd><span class="status ${statusClasse(meta.status)}">${esc(meta.status)}</span></dd></div>
  </dl>
</header>

<main>
${sumario ? '  <nav class="sumario"></nav>\n' : ""}${main}
</main>

<footer class="boaz-marca-rodape">
  <p class="boaz-eyebrow">Histórico</p>
  <table class="boaz-historico">
    <thead><tr><th>Versão</th><th>Data</th><th>Mudança</th></tr></thead>
    <tbody>
${hist}
    </tbody>
  </table>
  <div class="boaz-rodape-linha">
    <span>${esc(meta.projeto)} · ${esc(meta.versao)} · ${esc(meta.data)}</span>
    <span class="boaz-arquivo">${esc(caminho)}</span>
  </div>
</footer>
${sondaJs}</body>
</html>
`;
  if (saida) writeFileSync(saida, doc); else process.stdout.write(doc);
}

// ── conferência ─────────────────────────────────────────────
// âncoras de verdade: linha só com <a id>, ou no começo da célula do glossário; fora dos blocos de código
const semCercas = (t) => t.replace(/^(`{3,})\w*\n[\s\S]*?^\1\s*$/gm, "");
const listaAncoras = (t) => [...semCercas(t).matchAll(/^(?:\| )?<a id="([^"]+)"><\/a>/gm)].map((m) => m[1]);
const ancorasDe = (arquivo) => new Set(listaAncoras(readFileSync(arquivo, "utf8")));

function conferir(arquivo) {
  const erros = [], avisos = [];
  const { meta, corpo } = lerDocumento(arquivo);
  const caminho = relative(RAIZ, resolve(arquivo));
  for (const c of CAMPOS) if (!meta[c]) erros.push(`front matter sem "${c}"`);
  if (meta.documento && meta.documento !== basename(arquivo, ".md")) erros.push(`documento "${meta.documento}" ≠ nome do arquivo`);
  if (meta.versao && !/^v\d{2,}$/.test(meta.versao)) erros.push(`versão "${meta.versao}" fora do formato vNN`);
  if (meta.data && !/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}$/.test(meta.data)) erros.push(`data "${meta.data}" fora do formato AAAA-MM-DD HH:MM`);

  const linhas = corpo.split("\n");
  const fora = semCercas(corpo).split("\n");
  const carimbo = fora.find((l) => l.startsWith("> **"));
  if (!carimbo) erros.push("sem a linha do carimbo (> **projeto · tipo** · …)");
  else for (const c of ["documento", "versao", "data", "status"]) if (meta[c] && !carimbo.includes(meta[c])) erros.push(`carimbo sem ${c} "${meta[c]}"`);
  if (!fora.includes(`# ${meta.titulo}`)) erros.push(`sem o título "# ${meta.titulo}"`);
  const h = fora.findIndex((l) => l === "## Histórico");
  const primeira = h >= 0 ? fora.slice(h).find((l) => /^\| \*\*v/.test(l) || /^\| v\d/.test(l)) : null;
  if (!primeira) erros.push("sem histórico (## Histórico com a tabela Versão | Data | Mudança)");
  else if (!primeira.startsWith(`| **${meta.versao}** | ${meta.data} |`)) erros.push(`1ª linha do histórico ≠ ${meta.versao} · ${meta.data}`);
  const rod = fora.find((l) => l.startsWith("<sub>"));
  if (!rod || !rod.includes(`${meta.versao} · ${meta.data}`) || !rod.includes(caminho)) erros.push(`linha do rodapé ≠ "${meta.projeto} · ${meta.versao} · ${meta.data} · \`${caminho}\`"`);

  const vistas = new Set();
  const dir = dirname(resolve(arquivo));
  let cerca = null, gherkin = false;
  linhas.forEach((l, n) => {
    const f = l.match(/^(`{3,})(\w*)\s*$/);
    if (f && !cerca) { cerca = f[1]; gherkin = f[2] === "gherkin"; return; }
    if (cerca && l.trim() === cerca) { cerca = null; return; }
    if (cerca) {
      if (gherkin && [...l].length > 100) avisos.push(`linha ${n + 1}: Gherkin com ${[...l].length} caracteres (máx. 100)`);
      return;
    }
    for (const m of l.matchAll(/\]\(([^)\s]+)\)/g)) {
      const [alvo, frag] = m[1].split("#");
      if (/^[a-z]+:/.test(alvo)) continue;
      const arq = alvo ? resolve(dir, alvo) : resolve(arquivo);
      if (!existsSync(arq)) { erros.push(`linha ${n + 1}: não existe ${m[1]}`); continue; }
      if (frag && arq.endsWith(".md") && !ancorasDe(arq).has(frag)) erros.push(`linha ${n + 1}: âncora #${frag} não existe em ${alvo || "este documento"}`);
      if (/\/vista-/.test(alvo)) vistas.add(alvo);
    }
  });
  const ids = listaAncoras(corpo);
  const dup = ids.filter((x, k) => ids.indexOf(x) !== k);
  if (dup.length) erros.push(`âncoras repetidas: ${[...new Set(dup)].join(", ")}`);
  if (/\.html(#|\))/.test(semCercas(corpo))) erros.push("link para .html: documentos de requisito são .md");
  return { erros, avisos };
}

// ── CLI ─────────────────────────────────────────────────────
const [cmd, ...args] = process.argv.slice(2);
if (cmd === "html" && args[0]) {
  const pos = args.filter((a) => !a.startsWith("--"));
  html(pos[0], pos[1], args.includes("--sonda"));
} else if (cmd === "conferir" && args.length) {
  let falhas = 0;
  for (const a of args.filter((x) => !x.startsWith("--"))) {
    const { erros, avisos } = conferir(a);
    if (erros.length) falhas++;
    console.log(`${erros.length ? "✖" : avisos.length ? "▲" : "✔"} ${a}${avisos.length ? ` (${avisos.length} aviso${avisos.length > 1 ? "s" : ""})` : ""}`);
    for (const x of erros) console.log(`    erro: ${x}`);
    if (process.argv.includes("--avisos")) for (const x of avisos) console.log(`    aviso: ${x}`);
  }
  process.exit(falhas ? 1 : 0);
} else {
  console.error("Uso: node 0_Assets/requisitos/requisito.mjs html <doc.md> [saida.html] [--sonda]\n     node 0_Assets/requisitos/requisito.mjs conferir <doc.md>...");
  process.exit(2);
}
