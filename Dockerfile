# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["TLinkWebPortal/TLinkWebPortal/TLinkWebPortal.csproj", "TLinkWebPortal/TLinkWebPortal/"]
COPY ["TLinkWebPortal/TLinkWebPortal.Client/TLinkWebPortal.Client.csproj", "TLinkWebPortal/TLinkWebPortal.Client/"]
COPY ["TLinkWebPortal/TLink/DSC.TLink.csproj", "TLinkWebPortal/TLink/"]

# Restore dependencies
RUN dotnet restore "TLinkWebPortal/TLinkWebPortal/TLinkWebPortal.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/src/TLinkWebPortal/TLinkWebPortal"
RUN dotnet publish "TLinkWebPortal.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Expose ports (HTTP, HTTPS, and panel connection port)
EXPOSE 8080
EXPOSE 8443
EXPOSE 3072

# Copy published app
COPY --from=build /app/publish .

VOLUME /app

ENTRYPOINT ["dotnet", "TLinkWebPortal.dll"]