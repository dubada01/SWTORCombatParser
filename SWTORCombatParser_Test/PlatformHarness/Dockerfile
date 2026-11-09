FROM mcr.microsoft.com/dotnet/sdk:9.0
COPY --from=mcr.microsoft.com/dotnet/aspnet:8.0 /usr/share/dotnet/shared/Microsoft.NETCore.App /usr/share/dotnet/shared/Microsoft.NETCore.App
RUN apt-get update && apt-get install -y --no-install-recommends xvfb xauth libx11-6 libxext6 libxfixes3 libxtst6 libfontconfig1 openbox kwin-x11 kwin-wayland xwayland dbus-x11 && setcap -r /usr/bin/kwin_wayland && rm -rf /var/lib/apt/lists/*
WORKDIR /work
