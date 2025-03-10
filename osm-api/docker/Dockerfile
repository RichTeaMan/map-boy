FROM mcr.microsoft.com/dotnet/sdk:9.0

EXPOSE 8443
ENV DB_FILE_PATH=osm.db

COPY bin/published/ .
RUN dotnet dev-certs https
ENTRYPOINT [ "dotnet", "osm-api.dll", "--urls=https://*:8443;http://*:8080"]
