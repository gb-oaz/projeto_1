# PWA — parte técnica

Estado atual: a web **não tem** manifest, service worker nem plugin de PWA; a vista `-pwa` hoje cobre o layout da web instalada (classe compact). Adicionar capacidades de PWA é decisão do arquiteto e precisa de cenário no requisito (offline, instalação, atualização). Experiência: `ux-usabilidade/referencias/pwa-ux.md`.

## Quando houver cenário

- **Manifest** (`manifest.webmanifest`) por app: `name`, `short_name`, `start_url`, `scope`, `display: standalone`, `theme_color`/`background_color` vindos dos tokens gerados, ícones da identidade (`0_Assets/design/`, incluindo maskable).
- **Service worker**: plugin do ecossistema Vite (ex.: `vite-plugin-pwa`/Workbox) com versão fixada, via ADR.
  - App shell em cache (precache do build).
  - Dados da API: rede primeiro; nunca cache de resposta autenticada sem cenário que peça e regra de expiração.
  - Nunca cachear `/auth/*`.
- **Atualização**: `registerType: 'prompt'` — mostrar o aviso da vista e só recarregar quando a pessoa aceitar.
- **Offline**: detectar `navigator.onLine` + eventos `online`/`offline`, mas confirmar com falha real de rede; fila de ações só se o requisito pedir.
- **Áreas seguras**: `env(safe-area-inset-*)` e `viewport-fit=cover`.
- **Teste**: unitário da lógica de atualização/offline com eventos simulados; funcional no qa.
