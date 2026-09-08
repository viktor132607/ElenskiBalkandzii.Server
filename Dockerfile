FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ElenskiBalkandzii.Server/Directory.Packages.props ./
COPY ElenskiBalkandzii.Server/ElenskiBalkandzii.Server.API/ElenskiBalkandzii.Server.API.csproj ElenskiBalkandzii.Server.API/
COPY ElenskiBalkandzii.Server/ElenskiBalkandzii.Server.Data/ElenskiBalkandzii.Server.Data.csproj ElenskiBalkandzii.Server.Data/
COPY ElenskiBalkandzii.Server/ElenskiBalkandzii.Server.Domain/ElenskiBalkandzii.Server.Domain.csproj ElenskiBalkandzii.Server.Domain/

RUN dotnet restore ElenskiBalkandzii.Server.API/ElenskiBalkandzii.Server.API.csproj

COPY ElenskiBalkandzii.Server/. ./
RUN dotnet publish ElenskiBalkandzii.Server.API/ElenskiBalkandzii.Server.API.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:10000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

EXPOSE 10000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ElenskiBalkandzii.Server.API.dll"]
