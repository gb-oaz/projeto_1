# qa · backend · integração (14_estrutura-projetos RNF-191): roda os cenários
# de API contra API_URL ao iniciar, grava o JUnit XML em /resultados e sai com
# 0 só se tudo passou. O banco e o cache (MONGO_URL, REDIS_URL) só preparam o
# estado, e só são limpos com QA_PODE_LIMPAR=sim.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /suite
COPY Boaz.Qa.Integracao.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet build -c Release --no-restore
ENV DOTNET_NOLOGO=true DOTNET_CLI_TELEMETRY_OPTOUT=true
ENTRYPOINT ["dotnet", "test", "-c", "Release", "--no-build", \
  "--logger", "junit;LogFilePath=/resultados/integracao.xml", \
  "--logger", "console;verbosity=minimal"]
