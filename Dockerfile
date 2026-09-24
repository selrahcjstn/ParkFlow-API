FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ParkFlow.API/ParkFlow.API.csproj ParkFlow.API/
COPY ParkFlow.Application/ParkFlow.Application.csproj ParkFlow.Application/
COPY ParkFlow.Domain/ParkFlow.Domain.csproj ParkFlow.Domain/
COPY ParkFlow.Infrastructure/ParkFlow.Infrastructure.csproj ParkFlow.Infrastructure/
COPY ParkFlow.Persistence/ParkFlow.Persistence.csproj ParkFlow.Persistence/

RUN dotnet restore ParkFlow.API/ParkFlow.API.csproj

COPY . .
RUN dotnet publish ParkFlow.API/ParkFlow.API.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000

ENTRYPOINT ["dotnet", "ParkFlow.API.dll"]
