FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src
# Copy everything...
COPY ["./Roblox/", "/src/"]
# Restore everything
RUN dotnet restore "/src/Roblox.IntegrationTest/Roblox.IntegrationTest.csproj"&&\
    cd /src/Roblox.IntegrationTest && dotnet build;


CMD cd /src/Roblox.IntegrationTest/ && dotnet test;
