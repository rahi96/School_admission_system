# ---- Build stage ----
FROM node:22-alpine AS build
WORKDIR /app

# The backend URL the browser will call. Injected at build time because Vite
# inlines VITE_* variables when it bundles the app.
ARG VITE_API_BASE_URL=http://localhost:8080
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

COPY package*.json ./
RUN npm ci

COPY . ./
RUN npm run build

# ---- Runtime stage ----
FROM nginx:alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
