FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["./TiredDoctorManhattan/TiredDoctorManhattan.csproj", "TiredDoctorManhattan/"]
RUN dotnet restore "TiredDoctorManhattan/TiredDoctorManhattan.csproj"
COPY . .
WORKDIR "/src/TiredDoctorManhattan"
RUN dotnet build "TiredDoctorManhattan.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TiredDoctorManhattan.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "TiredDoctorManhattan.dll"]
