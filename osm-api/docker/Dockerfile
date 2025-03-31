FROM mcr.microsoft.com/dotnet/sdk:9.0

LABEL org.opencontainers.image.source=https://github.com/RichTeaMan/map-boy
LABEL org.opencontainers.image.description="Map Boy API"
LABEL org.opencontainers.image.licenses=GPL

EXPOSE 8443
ENV DB_FILE_PATH=osm.db

COPY bin/published/ .
COPY static-files/ static-files
RUN dotnet dev-certs https
ENTRYPOINT [ "dotnet", "osm-api.dll", "--urls=https://*:8443;http://*:8080"]
