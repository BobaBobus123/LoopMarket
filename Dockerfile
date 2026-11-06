# 1. Етап збірки (Build Stage)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Копіюємо проєктні файли для кешування NuGet-пакетів
COPY ["TradeStorm.Api/TradeStorm.Api.csproj", "TradeStorm.Api/"]
COPY ["TradeStorm.Api.Tests/TradeStorm.Api.Tests.csproj", "TradeStorm.Api.Tests/"]
RUN dotnet restore "TradeStorm.Api/TradeStorm.Api.csproj"

# Копіюємо решту файлів проєкту
COPY . .

# Збираємо та публікуємо у Release-режимі
WORKDIR "/src/TradeStorm.Api"
RUN dotnet publish "TradeStorm.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Фінальний етап запуску (Runtime Stage)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Налаштування порту для Render (Render автоматично маршрутизує на EXPOSEd порт 8080)
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TradeStorm.Api.dll"]
