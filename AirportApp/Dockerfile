FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["AirportApp.csproj", "./"]
RUN dotnet restore "AirportApp.csproj"
COPY . .
RUN dotnet publish "AirportApp.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .
RUN mkdir -p /app/DataProtectionKeys && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "AirportApp.dll"]
