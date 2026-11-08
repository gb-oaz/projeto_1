# .NET MAUI (mobile)

Requisitos: `01_stack` RNF-05, RNF-169, RNF-170, RNF-173, RNF-174; `14` RNF-183 a RNF-187, RNF-198; `03` RNF-26 (cobertura), RNF-22 (avisos como erro). Estrutura: `5_DEV/frontend/mobile/` (`Boaz.Mobile.slnx`, `apps/`, `core/`, `tests/`).

## Arquitetura

- Mesma hexagonal das outras frentes (RN-143): `core/<contexto>/<feature>/` com domínio, casos de uso e portas em C# puro; adaptadores (HTTP, armazenamento seguro) no app; composição só no `MauiProgram.cs` (RN-149).
- Um projeto MAUI por app, gerando Android e iOS (RNF-185).
- **MVVM**: página XAML ↔ ViewModel (estado observável e comandos) → caso de uso. ViewModel sem referência a controles; página sem regra.
- Estilos só dos tokens gerados em `Resources/Styles/Design/` (RNF-94, RNF-95: não editar gerados); nada de cor/tamanho solto no XAML.

## Integração e segurança

- `HttpClient` via `IHttpClientFactory`/injeção, endereço do backend por configuração (RNF-198).
- Token de acesso em memória; o que precisar persistir vai para `SecureStorage`, nunca `Preferences`.
- Atestação do app/rota do mobile conforme `05` RNF-77, RNF-78.

## Acessibilidade e plataforma

- `SemanticProperties.Description/Hint`, `SemanticProperties.HeadingLevel`; alvo ≥ 48 dp; fonte do sistema escalável.
- Safe areas, teclado sobre o campo, botão voltar do Android coerente com a navegação.

## Testes

- xUnit em `tests/` para domínio, casos de uso e ViewModels (sem dispositivo), cobertura RNF-26.
- Funcional em dispositivo/emulador é do qa (Appium, RNF-196, RNF-197).
- iOS só compila em macOS com Xcode verificado (RNF-170, RNF-173).
