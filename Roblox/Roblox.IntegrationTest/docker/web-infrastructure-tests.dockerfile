FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /src
COPY ["./Roblox/", "/src/"]

RUN dotnet restore "/src/Roblox.Web.Infrastructure.Tests/Roblox.Web.Infrastructure.Tests.csproj" && \
    dotnet build "/src/Roblox.Web.Infrastructure.Tests/Roblox.Web.Infrastructure.Tests.csproj" --no-restore

CMD dotnet test "/src/Roblox.Web.Infrastructure.Tests/Roblox.Web.Infrastructure.Tests.csproj" --no-build --logger "console;verbosity=minimal"
