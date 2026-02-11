# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["TLinkWebPortal/TLinkWebPortal.csproj", "TLinkWebPortal/"]
COPY ["TLinkWebPortal.Client/TLinkWebPortal.Client.csproj", "TLinkWebPortal.Client/"]
COPY ["TLink/DSC.TLink.csproj", "TLink/"]

# Restore dependencies
RUN dotnet restore "TLinkWebPortal/TLinkWebPortal.csproj"

# Copy all source code
COPY . .

# Build and publish
WORKDIR "/src/TLinkWebPortal"
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

ENTRYPOINT ["dotnet", "TLinkWebPortal.dll"]