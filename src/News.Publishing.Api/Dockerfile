ARG dotnet_version=8.0
########################################
#  First stage of multistage build
########################################
#  Use Build image with label `builder
########################################
FROM mcr.microsoft.com/dotnet/sdk:${dotnet_version}-alpine AS builder
ARG project_name
ARG run_codegen=true
# Setup working directory for project
WORKDIR /app

COPY ./${project_name}.sln ./
COPY ./src/${project_name}/${project_name}.csproj ./src/${project_name}/
COPY ./src/${project_name}.Api/${project_name}.Api.csproj ./src/${project_name}.Api/

#RUN dotnet restore ./${project_name}.csproj
RUN dotnet restore ./${project_name}.sln
COPY ./src/ ./src/

RUN dotnet run --project ./src/${project_name}.Api/${project_name}.Api.csproj -- codegen write

RUN ls
RUN ls -R ./src/${project_name}.Api/Internal

RUN dotnet build -c Release --no-restore ./${project_name}.sln

# Publish project to output folder
# and no build, as we did it already
WORKDIR /app/

RUN dotnet publish ./src/${project_name}.Api/${project_name}.Api.csproj -c Release --no-build -o out

########################################
#  Second stage of multistage build
########################################
#  Use other build image as the final one
#    that won't have source codes
########################################
FROM mcr.microsoft.com/dotnet/aspnet:${dotnet_version}-alpine
ARG project_name
ARG dotnet_version=8.0

EXPOSE 5267
EXPOSE 7029
WORKDIR /app

COPY --from=builder /app/out .

# Create an entry script to expand PROJECT_DLL dynamically
RUN echo -e '#!/bin/sh\nexec dotnet $PROJECT_DLL' > /app/entrypoint.sh \
    && chmod +x /app/entrypoint.sh \
    && sed -i 's/\r$//' /app/entrypoint.sh

ENV ASPNETCORE_ENVIRONMENT=Docker
# Set URL that App will be exposed
ENV ASPNETCORE_URLS="http://*:5267;http://*:7029"
ENV PROJECT_DLL="${project_name}.Api.dll"

ENTRYPOINT ["/app/entrypoint.sh"]