FROM mcr.microsoft.com/dotnet/core/sdk:3.1-alpine AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY *.cs .
COPY ["Connected Services", "."]
RUN dotnet publish -c release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/core/runtime:3.1-alpine
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["./stomrin-worker"]
