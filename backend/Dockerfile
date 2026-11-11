FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props ./
COPY SchoolAdmission.Api/SchoolAdmission.Api.csproj SchoolAdmission.Api/
COPY SchoolAdmission.Api/packages.lock.json SchoolAdmission.Api/
RUN dotnet restore SchoolAdmission.Api/SchoolAdmission.Api.csproj --locked-mode
COPY SchoolAdmission.Api/ SchoolAdmission.Api/
RUN dotnet publish SchoolAdmission.Api/SchoolAdmission.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "SchoolAdmission.Api.dll"]
