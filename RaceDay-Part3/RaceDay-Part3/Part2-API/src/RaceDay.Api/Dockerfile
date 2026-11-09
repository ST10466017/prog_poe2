FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Part2-API/src/RaceDay.Api/RaceDay.Api.csproj Part2-API/src/RaceDay.Api/
RUN dotnet restore Part2-API/src/RaceDay.Api/RaceDay.Api.csproj
COPY Part2-API/src/RaceDay.Api/ Part2-API/src/RaceDay.Api/
WORKDIR /src/Part2-API/src/RaceDay.Api
RUN dotnet publish RaceDay.Api.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","RaceDay.Api.dll"]