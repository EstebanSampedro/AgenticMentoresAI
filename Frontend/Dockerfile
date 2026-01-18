# Usar una imagen base de Node.js
FROM node:22 AS build
# Establecer el directorio de trabajo
WORKDIR /app

ARG VITE_API_BASE
ARG VITE_API_SignalR
ARG VITE_APPINSIGHTS_CONNECTION_STRING
# Establecer la variable de entorno para la API base
ENV VITE_API_BASE=${VITE_API_BASE}
ENV VITE_API_SignalR=${VITE_API_SignalR}
ENV VITE_APPINSIGHTS_CONNECTION_STRING=${VITE_APPINSIGHTS_CONNECTION_STRING}

# Copiar los archivos necesarios
COPY package*.json ./

# Eliminar node_modules y package-lock.json si existen
RUN rm -rf node_modules package-lock.json

# Instalar las dependencias
RUN npm install

# Copiar el resto del código de la aplicación
COPY . .

# Construir la aplicación
RUN npm run build

# Usar una imagen base de Node.js para servir la aplicación
FROM node:20-alpine

# Instalar 'serve' globalmente
RUN npm install -g serve

# Copiar los archivos construidos desde la etapa anterior
COPY --from=build /app/dist /app/dist

# Establecer el directorio de trabajo
WORKDIR /app

# Exponer el puerto en el que la aplicación se ejecutará
EXPOSE 3000

# Comando para servir la carpeta dist
CMD ["serve", "-s", "dist"]