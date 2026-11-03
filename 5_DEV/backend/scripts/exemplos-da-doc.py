#!/usr/bin/env python3
"""Exemplos de cada endpoint para o Swagger, tirados dos contratos de API.

Lê a versão mais recente de <modulo>-contrato-api em 2_Diagramas/ e, de cada
endereço (<g id="ep-…">), o JSON do PEDIDO e o de cada resposta. Erro que o
contrato descreve só pelo código ganha a mensagem exata da falha do core
(core/commons/usuario/<modulo>/domain/Falha*.cs). Grava
ms_mono/sistema/exemplos/<modulo>.json, que o DocumentacaoApi põe dentro de cada
endpoint. Rode ./5_DEV/backend/scripts/exemplos-da-doc.py depois de uma versão
nova de um contrato ou de uma mensagem do core; o teste
Todo_endpoint_traz_os_exemplos_do_contrato acusa endpoint sem exemplo.
"""
import json
import re
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parents[3]
BACKEND = RAIZ / "5_DEV" / "backend"
MODULOS = ["autenticacao", "usuarios", "autorizacao"]

# valores de exemplo para os trechos variáveis das mensagens do core
PESSOA = "Ana Lima"
MODULOS_DE_EXEMPLO = {"MOD-03": "Usuários", "MOD-05": "Quiz", "MOD-06": "Presença", "MOD-08": "Agenda", "MOD-12": "Relatórios"}


def falar(iso, em_horas):
    """PT15M → 15 min, PT12H → 12 h, P7D → 7 dias (como o Falar do core)."""
    m = re.fullmatch(r"P(?:(\d+)D)?(?:T(?:(\d+)H)?(?:(\d+)M)?)?", iso or "")
    if not m:
        return iso or ""
    dias, horas, minutos = (int(x or 0) for x in m.groups())
    total_min = dias * 1440 + horas * 60 + minutos
    if not em_horas and total_min % 1440 == 0:
        return f"{total_min // 1440} {'dia' if total_min == 1440 else 'dias'}"
    if total_min % 60 == 0:
        return f"{total_min // 60} h"
    return f"{total_min} min"


def chaves(valor, prefixo=""):
    if isinstance(valor, dict):
        for k, v in valor.items():
            yield prefixo + k
            yield from chaves(v, prefixo + k + ".")


def ultima_versao(legenda):
    versoes = sorted(RAIZ.glob(f"2_Diagramas/{legenda}_v*.svg"),
                     key=lambda p: int(re.search(r"_v(\d+)_", p.name).group(1)))
    if not versoes:
        sys.exit(f"✗ nenhuma versão de {legenda} em 2_Diagramas/")
    return versoes[-1]


def mensagens():
    """Código → primeira mensagem de cada módulo, das falhas do core."""
    por_modulo = {}
    for modulo in MODULOS:
        textos = {}
        for arquivo in (BACKEND / "core" / "commons" / "usuario" / modulo / "domain").glob("Falha*.cs"):
            fonte = arquivo.read_text(encoding="utf-8")
            for m in re.finditer(r'"([A-Z_]+)",\s*(?:[a-z]+\s*\?\s*)?\$?"((?:[^"\\]|\\.)*)"', fonte, re.S):
                codigo, texto = m.group(1), m.group(2).replace('\\"', '"')
                textos.setdefault(codigo, texto)
        por_modulo[modulo] = textos
    return por_modulo


def texto_das_linhas(grupo):
    """As linhas do endereço como (classe, texto), na ordem do desenho."""
    linhas = []
    for m in re.finditer(r'<text\b([^>]*)>(.*?)</text>', grupo, re.S):
        classe = re.search(r'class="([^"]+)"', m.group(1))
        classe = classe.group(1) if classe else ""
        texto = re.sub(r"<[^>]+>", "", m.group(2))
        texto = texto.replace("&amp;", "&").replace("&lt;", "<").replace("&gt;", ">").replace("&quot;", '"')
        linhas.append((classe, texto))
    return linhas


def json_a_partir(linhas, i):
    """Lê o bloco JSON que começa na linha i; devolve (valor, próxima linha)."""
    if i >= len(linhas) or linhas[i][1].strip() not in ("{", "["):
        return None, i
    pedaco, profundidade = [], 0
    while i < len(linhas):
        classe, texto = linhas[i]
        if classe != "mono":
            break
        pedaco.append(texto)
        profundidade += texto.count("{") + texto.count("[") - texto.count("}") - texto.count("]")
        i += 1
        if profundidade <= 0:
            break
    try:
        return json.loads("\n".join(pedaco)), i
    except json.JSONDecodeError:
        return None, i


def erro(codigo, descricao, extra, textos, chave, pedido):
    mensagem = textos.get(codigo, "")
    citada = re.search(r'"([^"]+)"', descricao)
    if citada and not mensagem:
        mensagem = citada.group(1)
    corpo = {"codigo": codigo, "mensagem": mensagem}
    if isinstance(extra, dict):
        corpo.update(extra)
    if codigo in ("DADOS_INVALIDOS", "FORA_DOS_LIMITES") and "campo" not in corpo:
        # o campo citado na linha do contrato, se for um campo do pedido
        citado = re.match(r"\s*([a-zA-Z.]+)(?=[\s,]|$)", descricao.split("·", 1)[1]) if "·" in descricao else None
        conhecidos = list(chaves(pedido))
        if citado and citado.group(1) not in ("o", "a", "campo"):
            corpo["campo"] = citado.group(1)
        else:
            corpo["campo"] = conhecidos[0] if conhecidos else "id"
    campo = corpo.get("campo", "")
    mod = corpo.get("mod") or (campo.split(".", 1)[1] if campo.startswith("mods.") else "MOD-06")
    if codigo == "DADOS_INVALIDOS" and campo.startswith("mods."):
        mensagem = textos.get("MODULO_INEXISTENTE", "O módulo {mod} não existe.")
    if codigo == "ESTADO_IGUAL" and "reativar" in chave:
        mensagem = "O usuário já está ativo."
    em_horas = campo not in ("staffApp", "participante")
    mensagem = re.sub(r"\{Falar\(minimo[^}]*\}", falar(corpo.get("minimo"), em_horas), mensagem)
    mensagem = re.sub(r"\{Falar\(maximo[^}]*\}", falar(corpo.get("maximo"), em_horas), mensagem)
    nome = PESSOA if codigo == "VERSAO_DESATUALIZADA" else MODULOS_DE_EXEMPLO.get(mod, "Presença")
    for variavel, valor in {"campo": campo, "mod": mod, "nome": nome, "pessoa": PESSOA}.items():
        mensagem = mensagem.replace("{" + variavel + "}", valor)
    corpo["mensagem"] = mensagem
    return {"codigo": corpo.pop("codigo"), "mensagem": corpo.pop("mensagem"), **corpo}


def exemplos_do_modulo(modulo, textos):
    svg = ultima_versao(f"{modulo}-contrato-api").read_text(encoding="utf-8")
    exemplos, com_login, sem_login = {}, set(), set()
    for m in re.finditer(r'<g id="ep-[^"]+">(.*?)\n  </g>', svg, re.S):
        linhas = texto_das_linhas(m.group(1))
        fortes = [t for c, t in linhas if c == "label-strong"]
        metodo, rota = fortes[0], fortes[1]
        chave = f"{metodo} {rota.lstrip('/').split('?')[0]}"
        pedido, respostas, i, secao = None, {}, 0, None
        while i < len(linhas):
            classe, texto = linhas[i]
            if classe == "boundary-label":
                secao = texto.strip()
                i += 1
                continue
            if secao == "PEDIDO" and classe == "mono" and pedido is None:
                valor, proxima = json_a_partir(linhas, i)
                if valor is not None:
                    pedido = valor
                    i = proxima
                    continue
            if secao == "RESPOSTAS" and classe == "chip-text" and re.fullmatch(r"\d{3}", texto.strip()):
                status = texto.strip()
                i += 1
                if i < len(linhas) and linhas[i][0] == "mono":
                    valor, proxima = json_a_partir(linhas, i)
                    if valor is not None:
                        respostas.setdefault(status, []).append({"nome": status, "corpo": valor})
                        i = proxima
                        continue
                    descricao = linhas[i][1]
                    codigo = re.match(r"([A-Z_]{4,})", descricao.strip())
                    if codigo:
                        extra, proxima = json_a_partir(linhas, i + 1)
                        respostas.setdefault(status, []).append(
                            {"nome": codigo.group(1), "corpo": erro(codigo.group(1), descricao, extra, textos, chave, pedido)})
                        i = proxima if extra is not None else i + 1
                        continue
                continue
            i += 1
        notas = " ".join(t for c, t in linhas if c == "muted")
        if "PUT com o mesmo corpo salva" in notas:
            # a caixa do GET também descreve o PUT: o corpo é o mesmo da leitura,
            # e os 422 (validação do corpo) só valem no PUT
            leitura = respostas.get("200", [{}])[0].get("corpo")
            escrita = json.loads(json.dumps(respostas))
            for exemplo in escrita.get("422", []):
                if exemplo["corpo"].get("campo") == "id":
                    exemplo["corpo"] = erro(exemplo["nome"], "", {"campo": next(iter(leitura))}, textos, chave, leitura)
            exemplos[f"PUT {chave.split(' ', 1)[1]}"] = {"pedido": leitura, "respostas": escrita}
            respostas = {s: l for s, l in respostas.items() if s != "422"}
        companheira = re.search(r"POST (/\S+) volta", notas)
        if companheira:
            outra = f"POST {companheira.group(1).lstrip('/')}"
            exemplos[outra] = {"pedido": pedido, "respostas": {
                s: [{**e, "corpo": erro(e["nome"], "", None, textos, outra, pedido)} if e["nome"] == "ESTADO_IGUAL" else e for e in l]
                for s, l in respostas.items()}}
        exemplos[chave] = {"pedido": pedido, "respostas": respostas}
        if "COM LOGIN" in (t for c, t in linhas if c == "chip-text"):
            com_login.update(k for k in exemplos if k not in sem_login)
        else:
            sem_login.update(k for k in exemplos if k not in com_login)

    # comum a todo endereço com login: o token (do filtro de acesso, igual em todos)
    for chave in com_login:
        exemplos[chave]["respostas"].setdefault("401", [
            {"nome": codigo, "corpo": erro(codigo, "", None, textos, chave, None)}
            for codigo in ("TOKEN_INVALIDO", "PERMISSOES_DESATUALIZADAS")])
    # o 404 de quem não existe vale para todos os métodos do mesmo endereço
    for chave, exemplo in exemplos.items():
        rota = chave.split(" ", 1)[1]
        if "{" not in rota or "404" in exemplo["respostas"]:
            continue
        irma = next((e for k, e in exemplos.items() if k.split(" ", 1)[1] == rota and "404" in e["respostas"]), None)
        if irma:
            exemplo["respostas"]["404"] = irma["respostas"]["404"]
    return {k: {"pedido": e["pedido"], "respostas": dict(sorted(e["respostas"].items()))} for k, e in exemplos.items()}


def main():
    textos = mensagens()
    destino = BACKEND / "ms_mono" / "sistema" / "exemplos"
    destino.mkdir(parents=True, exist_ok=True)
    for modulo in MODULOS:
        # a mensagem do próprio módulo primeiro; a dos outros cobre o que vem do filtro de acesso
        juntas = {}
        for outro in [modulo] + [m for m in MODULOS if m != modulo]:
            for codigo, texto in textos[outro].items():
                juntas.setdefault(codigo, texto)
        exemplos = exemplos_do_modulo(modulo, juntas)
        (destino / f"{modulo}.json").write_text(json.dumps(exemplos, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"✓ {len(exemplos)} endereços → sistema/exemplos/{modulo}.json")


if __name__ == "__main__":
    main()
