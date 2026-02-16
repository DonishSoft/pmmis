FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ["PMMIS.Web/PMMIS.Web.csproj", "PMMIS.Web/"]
COPY ["PMMIS.Application/PMMIS.Application.csproj", "PMMIS.Application/"]
COPY ["PMMIS.Domain/PMMIS.Domain.csproj", "PMMIS.Domain/"]
COPY ["PMMIS.Infrastructure/PMMIS.Infrastructure.csproj", "PMMIS.Infrastructure/"]
RUN dotnet restore "PMMIS.Web/PMMIS.Web.csproj"
COPY . .
WORKDIR "/src/PMMIS.Web"
RUN dotnet build "PMMIS.Web.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "PMMIS.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PMMIS.Web.dll"]
