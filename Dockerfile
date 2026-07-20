FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ImpactXv1/Prueba1.csproj ImpactXv1/
RUN dotnet restore ImpactXv1/Prueba1.csproj

COPY . .
RUN dotnet build ImpactXv1/Prueba1.csproj --no-restore --configuration Release

FROM build AS publish
RUN dotnet publish ImpactXv1/Prueba1.csproj --no-build --configuration Release --output /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "Prueba1.dll"]
