FROM node:22-bookworm-slim AS frontend-build
WORKDIR /frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /source
COPY src/WeatherApp/WeatherApp.csproj src/WeatherApp/
RUN dotnet restore src/WeatherApp/WeatherApp.csproj
COPY src/WeatherApp/ src/WeatherApp/
RUN dotnet publish src/WeatherApp/WeatherApp.csproj -c Release --no-restore -o /publish
COPY --from=frontend-build /frontend/dist/ /publish/wwwroot/

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=backend-build /publish/ ./
RUN mkdir -p /app/data && chown -R app:app /app/data
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ConnectionStrings__Weather="Data Source=/app/data/weather.db"
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "WeatherApp.dll"]
