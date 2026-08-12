FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/Detara.Web/Detara.Web.csproj
RUN dotnet publish src/Detara.Web/Detara.Web.csproj -c Release -o /app --no-restore

FROM nginx:1.29-alpine AS final
COPY src/Detara.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/wwwroot /usr/share/nginx/html
EXPOSE 8080
