FROM debian:bullseye-slim
RUN dpkg --add-architecture i386
RUN apt-get update && apt-get install -y --no-install-recommends \
    wget gnupg ca-certificates \
    wine wine64 wine32 \
    libwine libwine:i386 \
    xvfb xauth procps supervisor \
    && rm -rf /var/lib/apt/lists/*
RUN wget -qO- https://deb.nodesource.com/setup_20.x | bash - \
    && apt-get install -y nodejs \
    && rm -rf /var/lib/apt/lists/*

ENV WINEPREFIX=/root/.wine
ENV WINEARCH=win64
ENV DISPLAY=:99

# pre init wine so it doesnt do it at every run
RUN set -ex; \
    xvfb-run wineboot --init; \
    # wait for wineserver to finish setup
    while pgrep wineserver > /dev/null; do sleep 1; done

COPY supervisord.conf /etc/supervisor/conf.d/supervisord.conf 

WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build

#CMD ["node", "./dist/index.js"]
ENTRYPOINT ["/usr/bin/supervisord"]