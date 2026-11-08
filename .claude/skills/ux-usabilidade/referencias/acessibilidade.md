# Acessibilidade digital — WCAG 2.2 nível AA

Vale para vistas (uxui), código web e mobile (frontend) e testes funcionais (qa).

## Perceptível

- **Contraste**: texto normal ≥ 4,5:1; texto grande (≥ 24 px, ou 18,66 px negrito) ≥ 3:1; ícones, bordas de campo e foco ≥ 3:1. Use os pares de papel do M3 (`on-X` sobre `X`), que já são calculados para isso — e meça quando combinar fora do par.
- **Cor não é o único sinal**: erro tem ícone e texto, não só vermelho; status tem rótulo.
- **Texto alternativo**: imagem informativa com `alt`/descrição; decorativa (mascote decorativo, RNF-113) oculta do leitor de tela.
- **Redimensionar**: 200 % de zoom e texto aumentado sem perder conteúdo; reflow em 320 px de largura sem rolagem horizontal.

## Operável

- **Teclado**: tudo alcançável e operável por teclado (web), ordem de foco lógica, sem armadilha de foco; diálogo prende e devolve o foco.
- **Foco visível** e não encoberto por barra fixa (2.4.11).
- **Alvo de toque**: ≥ 24 × 24 px (2.5.8); M3 recomenda 48 × 48 dp — adote 48 no mobile.
- **Movimento**: animação respeita "reduzir movimento"; nada pisca mais de 3 vezes por segundo; selos animados (RN-249) e mascotes com versão estática quando o sistema pedir.
- **Tempo**: aviso antes de a sessão expirar quando o requisito permitir estender.
- **Arrastar** sempre com alternativa de toque simples (2.5.7).

## Compreensível

- Idioma da página declarado (`lang="pt-BR"`).
- Rótulo visível em todo campo, ligado ao campo; instrução antes, erro junto do campo e anunciado.
- Navegação e nomes consistentes entre telas (3.2.6 ajuda consistente).
- Não peça de novo dado já informado no mesmo fluxo (3.3.7); autenticação sem teste cognitivo (3.3.8).

## Robusto

- HTML semântico (`button`, `nav`, `main`, `h1…h6`, `label`); ARIA só quando o HTML não basta.
- Nome, papel e estado expostos (aria-expanded, aria-selected, aria-invalid).
- No MAUI: `SemanticProperties.Description`, `SemanticProperties.Hint`, `HeadingLevel`.
- Mensagens de status com `role="status"`/`aria-live` sem tirar o foco.

## Como verificar

- Contraste: calcule pelo valor dos tokens (não a olho).
- Web: navegação só por teclado; leitor de tela (VoiceOver/NVDA) nos fluxos principais; axe no funcional do qa quando a suíte tiver.
- Mobile: VoiceOver (iOS) e TalkBack (Android); tamanho de fonte do sistema no máximo.
