# uso (na raiz do repositório):
#   python3 5_DEV/qa/backend/integracao/scripts/copiar-cenarios.py \
#     <requisito.html> <saida.feature> <Funcionalidade>
# Copia os Cenários de API do requisito para o .feature, cada cenário com a tag do ID.
# Nome de cenário repetido no requisito ganha o ID no fim ("nome curto repetido · RF-153"):
# o Reqnroll não aceita dois cenários com o mesmo nome no mesmo .feature.
import re, sys, html
arq, saida, nome = sys.argv[1:4]
s = open(arq).read()
versao = re.search(r'<meta name="boaz:versao" content="(v\d+)">', s).group(1)
sec = re.search(r'<section id="cenarios-api">(.*?)</section>', s, re.S).group(1)
out = ['# language: pt',
       f'# Cenários de API de {arq} ({versao}), copiados do requisito:',
       '# mudou lá, muda aqui. Cada cenário leva a tag do ID que cobre.',
       f'Funcionalidade: {nome}', f'  {arq} · Cenários de API', '']
vistos = set()
for m in re.finditer(r'<article class="criterio" id="([^"]+)">\s*<h3>.*?</h3>\s*<pre class="gherkin">(.*?)</pre>', sec, re.S):
    tag = '@' + re.sub(r'-api$', '', m.group(1))
    g = html.unescape(m.group(2))
    for bloco in re.split(r'\n\n(?=(?:Cenário|Esquema do Cenário):)', g):
        titulo = bloco.split('\n', 1)[0]
        if titulo in vistos:
            bloco = bloco.replace(titulo, f'{titulo} · {tag[1:]}', 1)
        vistos.add(titulo)
        out.append(f'  {tag}')
        out += [('  ' + l) if l else '' for l in bloco.split('\n')]
        out.append('')
open(saida, 'w').write('\n'.join(out).rstrip() + '\n')
print(saida, versao)
