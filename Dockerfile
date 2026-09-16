FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY saviaup.backend.admin.sln ./
COPY saviaup.backend.admin.Domain/*.csproj saviaup.backend.admin.Domain/
COPY saviaup.backend.admin.Application/*.csproj saviaup.backend.admin.Application/
COPY saviaup.backend.admin.Infrastructure/*.csproj saviaup.backend.admin.Infrastructure/
COPY saviaup.backend.admin.Api/*.csproj saviaup.backend.admin.Api/
COPY tests/saviaup.backend.admin.Application.Tests/*.csproj tests/saviaup.backend.admin.Application.Tests/
COPY tests/saviaup.backend.admin.IntegrationTests/*.csproj tests/saviaup.backend.admin.IntegrationTests/
RUN dotnet restore saviaup.backend.admin.sln
COPY . .
RUN dotnet publish saviaup.backend.admin.Api/saviaup.backend.admin.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app .
ENTRYPOINT ["dotnet", "saviaup.backend.admin.Api.dll"]
