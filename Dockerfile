FROM node:22-alpine AS frontend-build
WORKDIR /src/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
ARG VITE_API_BASE_URL=
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS backend-build
WORKDIR /src
COPY backend/ ./backend/
RUN dotnet publish backend/WordGame.Api/WordGame.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=backend-build /out ./
COPY --from=frontend-build /src/frontend/dist ./wwwroot
ENV ASPNETCORE_ENVIRONMENT=Production ASPNETCORE_HTTP_PORTS=10000
EXPOSE 10000
USER $APP_UID
ENTRYPOINT ["dotnet", "WordGame.Api.dll"]
