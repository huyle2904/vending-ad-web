FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj VendingAdSolution/VendingAdSystem/
RUN dotnet restore VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj

COPY . .
RUN dotnet publish VendingAdSolution/VendingAdSystem/VendingAdSystem.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

RUN mkdir -p /data/uploads

EXPOSE 8080
ENTRYPOINT ["dotnet", "VendingAdSystem.dll"]
