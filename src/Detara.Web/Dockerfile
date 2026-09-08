FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:4beef5b8919dcaa2dc924233bd069257e883cc7a061e09088a97d152d6a48510 AS build
WORKDIR /source
COPY . .
ARG DETARA_API_ORIGIN=https://api.detara.com.br
# Configuração pública antes do publish: os hashes do service worker continuam corretos.
RUN echo "$DETARA_API_ORIGIN" | grep -Eq '^https://[a-zA-Z0-9.-]+(:[0-9]+)?$' \
    && sed -i "s|\"BaseUrl\": \"\"|\"BaseUrl\": \"$DETARA_API_ORIGIN/\"|" src/Detara.Web/wwwroot/appsettings.json \
    && sed -i "s|connect-src 'self'|connect-src 'self' $DETARA_API_ORIGIN|" src/Detara.Web/nginx.conf
RUN dotnet restore src/Detara.Web/Detara.Web.csproj
RUN rm -f /source/src/Detara.Web/wwwroot/appsettings.Development.json \
    && dotnet publish src/Detara.Web/Detara.Web.csproj -c Release -o /app --no-restore

FROM nginxinc/nginx-unprivileged:1.30.4-alpine-slim@sha256:3a4485bf084957d56674ee22db07d77d5a281418815c5852827419d6d629d440 AS final
COPY --from=build /source/src/Detara.Web/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/wwwroot /usr/share/nginx/html
EXPOSE 8080
