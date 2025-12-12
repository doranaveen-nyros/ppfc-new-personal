FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ppfc.API/*.csproj ppfc.API/
COPY ppfc.DTO/*.csproj ppfc.DTO/

RUN dotnet restore ppfc.API/ppfc.API.csproj
COPY . .
WORKDIR /src/ppfc.API
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "ppfc.API.dll"]
