# Use the official .NET 9 SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build

# Set the working directory inside the container
WORKDIR /app

# Copy the solution and project files
COPY . .

# Restore dependencies
RUN dotnet restore UI/Academikus.AgenteInteligenteMentoresTareas.WebApi.csproj

# Copy the rest of the application files
COPY . ./

# Build the application
RUN dotnet publish UI/Academikus.AgenteInteligenteMentoresTareas.WebApi.csproj -c Release -o /out

# Use the official .NET 10 runtime image for running the application
FROM mcr.microsoft.com/dotnet/aspnet:10.0

# RUN apt-get update && apt-get upgrade -y
RUN apt-get update && apt-get install -y \
    krb5-user \
    kstart \
    && rm -rf /var/lib/apt/lists/*

# Set the working directory inside the runtime container
WORKDIR /app

# Copy the published output from the build stage
COPY --from=build /out .

# Expose the port the application runs on
EXPOSE 8080

# Set the entry point for the container
ENTRYPOINT ["k5start", "-U", "-f", "/etc/keytabs/udlamentoresai.keytab", "-K", "60", "-l", "10h","dotnet", "Academikus.AgenteInteligenteMentoresTareas.WebApi.dll"]