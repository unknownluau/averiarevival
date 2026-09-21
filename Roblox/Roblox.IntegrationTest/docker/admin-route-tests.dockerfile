FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src
COPY ["./Roblox/", "/src/"]

RUN dotnet restore "/src/Services/Roblox.Services.Admin.Tests/Roblox.Services.Admin.Tests.csproj" && \
    dotnet build "/src/Services/Roblox.Services.Admin.Tests/Roblox.Services.Admin.Tests.csproj" --no-restore

CMD dotnet test "/src/Services/Roblox.Services.Admin.Tests/Roblox.Services.Admin.Tests.csproj" --no-build --logger "console;verbosity=minimal"
