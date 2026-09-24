FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ParkFlow.API/ParkFlow.API.csproj", "ParkFlow.API/"]
COPY ["ParkFlow.Application/ParkFlow.Application.csproj", "ParkFlow.Application/"]
COPY ["ParkFlow.Infrastructure/ParkFlow.Infrastructure.csproj", "ParkFlow.Infrastructure/"]
COPY ["ParkFlow.Persistence/ParkFlow.Persistence.csproj", "ParkFlow.Persistence/"]
COPY ["ParkFlow.Domain/ParkFlow.Domain.csproj", "ParkFlow.Domain/"]
RUN dotnet restore "ParkFlow.API/ParkFlow.API.csproj"

COPY . .
WORKDIR /src/ParkFlow.API
RUN dotnet publish "ParkFlow.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 10000
ENV ASPNETCORE_URLS=http://0.0.0.0:10000

ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} dotnet ParkFlow.API.dll"]
