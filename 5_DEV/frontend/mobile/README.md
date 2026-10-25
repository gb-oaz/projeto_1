# mobile

A solution `Boaz.Mobile.slnx`, com um projeto .NET MAUI por app (`hub` e
`quest`), cada um gerando o Android e o iOS, na estrutura hexagonal de
[`14_estrutura-projetos`](../../../1_Requisitos/14_estrutura-projetos.html)
(RN-143 a RN-149, RNF-183 a RNF-187).

```
5_DEV/frontend/mobile/
├── Boaz.Mobile.slnx
├── core/                         domínio, casos de uso, portas e componentes, sem I/O
│   ├── commons/                  Boaz.Mobile.Core.Commons (o que os contextos compartilham)
│   │   ├── components/           componentes comuns (ContentView e ViewModel)
│   │   └── Resources/Styles/Design/   cópia gerada do design system (não edite)
│   ├── hub/                      Boaz.Mobile.Core.Hub   ─┐ importam só o Commons
│   └── quest/                    Boaz.Mobile.Core.Quest ─┘
│       └── <feature>/            components/ · domain/ · usecases/ · ports/in/ · ports/out/
├── apps/<app>/                   Boaz.Mobile.Hub e Boaz.Mobile.Quest (Android e iOS)
│   ├── MauiProgram.cs            ponto de composição (liga portas e adaptadores)
│   ├── App.xaml                  junta os tokens do Commons
│   ├── Platforms/                Android e iOS
│   └── infra/
│       ├── in/<contexto>/        Shell e páginas (chamam ports/in)
│       └── out/<contexto>/       clientes HTTP e armazenamento seguro
└── tests/                        espelha core/ e apps/, um xUnit por projeto (alvo net10.0)
```

O `Directory.Build.props` desta pasta tem as propriedades comuns: as TFMs (os
apps usam `$(MobileAppTargetFrameworks)` e as bibliotecas
`$(MobileCoreTargetFrameworks)`, que soma o `net10.0` dos testes), o `ApiUrl`
e a checagem do Xcode desligada. As cópias de `core/commons/Resources/Styles/Design/` vêm de
`pnpm run gerar` em `0_Assets/design/`. As features entram com o módulo que as
cria, em `snake_case`, com o mesmo nome nas três frentes.

```bash
# na pasta 5_DEV/frontend/mobile, com os workloads do global.json
MobileSemIos=true dotnet build      # só Android (como no Linux)
MobileSemIos=true dotnet test
dotnet build apps/hub -f net10.0-ios -c Debug   # simulador iOS, no macOS
```

## Rodar no emulador e no simulador

Na pasta `5_DEV/frontend/mobile`: compile, ligue o aparelho e rode o app.
`-t:Run` instala e abre o app no aparelho ligado (e compila de novo o que
mudou). Troque `hub` por `quest` para o outro app.

**Android**, com o emulador do SDK do Android (`-list-avds` mostra os
disponíveis):

```bash
# 1. compilar
MobileSemIos=true dotnet build apps/hub -f net10.0-android

# 2. ligar o emulador e esperar o emulator-5554 aparecer como device
~/Library/Android/sdk/emulator/emulator -avd Pixel_5 &
~/Library/Android/sdk/platform-tools/adb devices

# 3. instalar e abrir o app
MobileSemIos=true dotnet build apps/hub -f net10.0-android -t:Run

# 4. desligar o emulador
~/Library/Android/sdk/platform-tools/adb emu kill
```

**iOS**, no macOS, com um simulador do Xcode. O aparelho é escolhido pelo
UDID em `_DeviceName`; o `-t:Run` liga o simulador se ele estiver desligado:

```bash
# 1. compilar
dotnet build apps/hub -f net10.0-ios

# 2. escolher o aparelho: a lista mostra nome e UDID de cada simulador
xcrun simctl list devices available
UDID=$(xcrun simctl list devices available | grep -m1 "iPhone 17 Pro (" | grep -oE '[0-9A-F-]{36}')

# 3. mostrar a janela, instalar e abrir o app no aparelho escolhido
open -a Simulator
dotnet build apps/hub -f net10.0-ios -t:Run -p:_DeviceName=:v2:udid=$UDID

# 4. desligar o simulador
xcrun simctl shutdown $UDID
```

Com o repositório numa pasta do iCloud Drive (`~/Documents`, `~/Desktop`), a
sincronização grava atributos nas pastas do `.app` do iOS e o `codesign`
recusa o pacote ("resource fork, Finder information, or similar detritus not
allowed"); o `Directory.Build.props` desta pasta limpa esses atributos logo
antes da assinatura.
