FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src
COPY ["./Roblox/", "/src/"]

RUN dotnet restore "/src/Services/Roblox.Services.Avatar.Tests/Roblox.Services.Avatar.Tests.csproj" && \
    dotnet build "/src/Services/Roblox.Services.Avatar.Tests/Roblox.Services.Avatar.Tests.csproj" --no-restore

CMD dotnet test "/src/Services/Roblox.Services.Avatar.Tests/Roblox.Services.Avatar.Tests.csproj" --no-build --logger "console;verbosity=minimal"
