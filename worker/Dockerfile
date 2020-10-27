FROM j0057.azurecr.io/dotnet/core/sdk:3.1-alpine AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY *.cs .
COPY ["Connected Services", "."]
RUN dotnet publish -c release -o /app --no-restore

FROM j0057.azurecr.io/dotnet/core/runtime:3.1-alpine
WORKDIR /app
USER 100:101
COPY --from=build /app .
ENTRYPOINT ["./stomrin-worker"]
