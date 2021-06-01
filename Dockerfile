FROM mcr.microsoft.com/dotnet/sdk:6.0 as sdk
ARG VERSION=1.0.0
WORKDIR /usr/local/src
ADD . .
RUN dotnet publish -c Release -p:Version=${VERSION} -o /usr/local/bin

FROM mcr.microsoft.com/dotnet/aspnet:6.0
WORKDIR /usr/local/bin
COPY --from=sdk /usr/local/bin ./
ENTRYPOINT [ "dotnet", "Project.dll" ]

