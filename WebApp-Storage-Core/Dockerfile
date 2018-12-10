FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /src
COPY ["WebApp-Storage-Core.csproj", "WebApp-Storage-Core/"]
RUN dotnet restore "WebApp-Storage-Core/WebApp-Storage-Core.csproj"
WORKDIR "/src/WebApp-Storage-Core"
COPY . .
RUN dotnet build "WebApp-Storage-Core.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "WebApp-Storage-Core.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "WebApp-Storage-Core.dll"]