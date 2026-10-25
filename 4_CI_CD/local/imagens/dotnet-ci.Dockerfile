# Imagem da verificação do backend no CI local (RNF-161): o SDK .NET na
# versão exata do global.json (passada pelo ci.sh), instalado pelo
# dotnet-install.sh, como faz o setup-dotnet do GitHub Actions. A Microsoft só
# publica imagem do SDK para a faixa mais nova (ex.: 10.0.4xx), não para cada
# versão fixada no global.json.
# O conjunto de workloads do workloadVersion também entra (só os manifestos,
# sem workload nenhum): com ele fixado no global.json, o build falha onde o
# conjunto não está instalado, mesmo num projeto que não usa workloads.
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0

ARG DOTNET_SDK
ARG DOTNET_WORKLOADS
ENV DOTNET_ROOT=/usr/share/dotnet \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates \
 && rm -rf /var/lib/apt/lists/* \
 && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --version "$DOTNET_SDK" --install-dir /usr/share/dotnet \
 && ln -s /usr/share/dotnet/dotnet /usr/bin/dotnet \
 && rm /tmp/dotnet-install.sh \
 && if [ -n "$DOTNET_WORKLOADS" ]; then dotnet workload update --version "$DOTNET_WORKLOADS"; fi
