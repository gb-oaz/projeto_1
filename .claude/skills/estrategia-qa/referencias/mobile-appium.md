# Funcional do mobile — Appium + C#/xUnit

Suíte: `5_DEV/qa/frontend/mobile/funcional/` (RNF-196, RNF-197). Roda na máquina por `executar.sh` (por app e plataforma) com `PLATAFORMA`, `APP`, `APP_PACOTE`, `APPIUM_URL`, `API_URL`, `API_URL_DISPOSITIVO`, `RESULTADOS` (README do qa). Appium e drivers (UiAutomator2, XCUITest) com versão exata no `package.json` da pasta.

## Boas práticas

- Localizar por **AutomationId** (no MAUI, `AutomationId` no XAML — combinar com o `deboaz-frontend`) ou por acessibilidade (`SemanticProperties.Description`); nunca XPath por posição.
- Sessão com `appium:enforceAppInstall` (o pacote muda a cada execução).
- Esperas explícitas por elemento/estado; sem `Thread.Sleep`.
- Preparo de dados pela API (`API_URL`); o app enxerga o backend por `API_URL_DISPOSITIVO` (`10.0.2.2` no emulador Android).
- Um teste por cenário de tela, com o ID no nome; independentes entre si.
- Page Object (ou Screen Object) por tela para concentrar localizadores.
- Android no CI (emulador); iOS só na máquina macOS (`ci.sh --qa mobile`).
- Verificar também acessibilidade básica: elementos com descrição, alvo tocável.
