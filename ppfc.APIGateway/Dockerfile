FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ppfc.APIGateway/*.csproj ppfc.APIGateway/
COPY ppfc.DTO/*.csproj ppfc.DTO/

RUN dotnet restore ppfc.APIGateway/ppfc.APIGateway.csproj
COPY . .
WORKDIR /src/ppfc.APIGateway
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "ppfc.APIGateway.dll"]
