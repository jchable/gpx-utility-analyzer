# ============================================
# Stage 1: Build React app
# ============================================
FROM node:26-alpine AS build
WORKDIR /app

COPY ui/client/package.json ui/client/package-lock.json* ./
RUN npm install

COPY ui/client/ ./
RUN npm run build

# ============================================
# Stage 2: Serve with nginx
# ============================================
FROM nginx:alpine AS runtime

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config
COPY ui/client/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
