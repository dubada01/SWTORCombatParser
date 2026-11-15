FROM mcr.microsoft.com/dotnet/runtime-deps:8.0-bookworm-slim
RUN apt-get update && apt-get install -y --no-install-recommends \
    python3 xvfb xauth libx11-6 libxext6 libxfixes3 libxtst6 \
    libfontconfig1 libice6 libsm6 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
