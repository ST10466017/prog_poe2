FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Part3-MVC/RaceDay.Mvc.csproj Part3-MVC/
RUN dotnet restore Part3-MVC/RaceDay.Mvc.csproj
COPY Part3-MVC/ Part3-MVC/
WORKDIR /src/Part3-MVC
RUN dotnet publish RaceDay.Mvc.csproj -c Release -o /app/publish /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","RaceDay.Mvc.dll"]