# Imagem backend-mono (14_estrutura-projetos RNF-177, 04_publicacao-imagens):
# publica o ms_mono e roda no runtime ASP.NET Core, escutando na porta 8080,
# com /health para a prontidão. Imagens com tag major.minor (RNF-19); o
# global.json fica fora deste contexto de build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish ms_mono/Boaz.MsMono.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Boaz.MsMono.dll"]
