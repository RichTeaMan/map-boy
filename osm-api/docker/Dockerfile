FROM mcr.microsoft.com/dotnet/aspnet:9.0

EXPOSE 8443
ENV DB_FILE_PATH=osm.db

COPY bin/published/ .
ENTRYPOINT [ "dotnet", "osm-api.dll" "--urls=https://localhost:8443"]
