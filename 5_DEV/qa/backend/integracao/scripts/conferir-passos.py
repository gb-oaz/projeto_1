# uso (em 5_DEV/qa/backend/integracao/):
#   python3 scripts/conferir-passos.py
# Confere, sem rodar os testes, cada frase de cada .feature contra os passos
# [Given]/[When]/[Then] dos .cs. O Reqnroll só acusa estes erros quando o
# cenário chega na frase, e no TDD o cenário costuma parar antes, no preparo:
#   sem passo     nenhuma expressão casa com a frase
#   ambíguo       duas ou mais casam
#   parâmetros    os grupos que casaram não batem com os parâmetros do método
#                 (o Reqnroll não passa o grupo opcional que não casou)
# Sai com código 1 se achar algum.
import glob, re, sys

passos = []
for arq in glob.glob('**/*.cs', recursive=True):
    if arq.endswith('.feature.cs') or '/obj/' in arq or '/bin/' in arq:
        continue
    texto = open(arq).read()
    for m in re.finditer(r'^\s*\[(Given|When|Then)\(@"((?:[^"]|"")*)"\)\]', texto, re.M):
        # depois dos atributos, a primeira declaração de método
        assinatura = re.search(r'^\s*(?:public|private|internal)[^(\n]*\(([^)]*)\)', texto[m.end():], re.M)
        params = [p for p in assinatura.group(1).split(',') if p.strip()] if assinatura else []
        linha = texto[:m.start()].count('\n') + 2
        passos.append((m.group(1), re.compile(m.group(2).replace('""', '"')), f'{arq}:{linha}', len(params)))

tipos = {'Dado': 'Given', 'Quando': 'When', 'Então': 'Then'}
erros = 0
for feature in sorted(glob.glob('**/*.feature', recursive=True)):
    problemas = set()
    for bloco in re.split(r'\n(?=\s*(?:@\S+\s*\n\s*)?(?:Cenário|Esquema do Cenário):)', open(feature).read()):
        linhas = bloco.split('\n')
        frases, exemplos, tipo, i = [], [], None, 0
        while i < len(linhas):
            l = linhas[i].strip()
            m = re.match(r'(Dado|Quando|Então|E|Mas)\s+(.*)', l)
            if m:
                tipo = tipos.get(m.group(1), tipo)
                doc = i + 1 < len(linhas) and linhas[i + 1].strip().startswith('"""')
                frases.append((tipo, m.group(2), doc))
                if doc:
                    i += 2
                    while not linhas[i].strip().startswith('"""'):
                        i += 1
            elif l.startswith('Exemplos'):
                colunas = [c.strip() for c in linhas[i + 1].strip().strip('|').split('|')]
                i += 2
                while i < len(linhas) and linhas[i].strip().startswith('|'):
                    exemplos.append(dict(zip(colunas, [c.strip() for c in linhas[i].strip().strip('|').split('|')])))
                    i += 1
                continue
            i += 1
        for exemplo in exemplos or [{}]:
            for tipo, frase, doc in frases:
                for coluna, valor in exemplo.items():
                    frase = frase.replace(f'<{coluna}>', valor)
                casam = [p for p in passos if p[0] == tipo and p[1].search(frase)]
                if not casam:
                    problemas.add(f'sem passo     {tipo} {frase}')
                elif len(casam) > 1:
                    problemas.add(f'ambíguo       {tipo} {frase} → {", ".join(p[2] for p in casam)}')
                else:
                    grupos = [g for g in casam[0][1].search(frase).groups() if g is not None] + ([''] if doc else [])
                    if len(grupos) != casam[0][3]:
                        problemas.add(f'parâmetros    {frase} → {casam[0][2]}: {len(grupos)} valores, {casam[0][3]} parâmetros')
    print(f'{feature}: {len(problemas) or "ok"}')
    for p in sorted(problemas):
        print('  ' + p)
    erros += len(problemas)
sys.exit(1 if erros else 0)
