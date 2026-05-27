FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY global.json ./
COPY VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj VendingAdSolution/VendingAdSystem/
COPY VendingAdSolution/VendingAd.Application/VendingAd.Application.csproj VendingAdSolution/VendingAd.Application/
COPY VendingAdSolution/VendingAd.Contracts/VendingAd.Contracts.csproj VendingAdSolution/VendingAd.Contracts/
COPY VendingAdSolution/VendingAd.Domain/VendingAd.Domain.csproj VendingAdSolution/VendingAd.Domain/
COPY VendingAdSolution/VendingAd.Infrastructure/VendingAd.Infrastructure.csproj VendingAdSolution/VendingAd.Infrastructure/
RUN dotnet restore VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj

COPY . .
RUN dotnet publish VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

RUN apt-get update \
    && apt-get install -y --no-install-recommends ffmpeg \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /data/uploads

EXPOSE 8080
ENTRYPOINT ["dotnet", "VendingAdSystem.dll"]
