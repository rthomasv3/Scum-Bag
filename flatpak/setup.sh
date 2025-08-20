#!/bin/bash
unzip ScumBag-Linux.zip
mkdir -p /app/lib /app/bin

install -Dm755 ScumBag /app/bin/ScumBag.real
install -Dm644 libnfd.so /app/lib/libnfd.so
install -Dm644 libwebview.so /app/lib/libwebview.so

# Find and create webkit symlink
for lib in /usr/lib*/libwebkit2gtk-4.1.so.0 /usr/lib/*/libwebkit2gtk-4.1.so.0; do 
    if [ -f "$lib" ]; then 
        ln -sf "$lib" /app/lib/libwebkit2gtk-4.1.so
        break
    fi
done

# Create wrapper script
cat > /app/bin/scum-bag << 'EOF'
#!/bin/sh
export LD_LIBRARY_PATH=/app/lib:$LD_LIBRARY_PATH
exec /app/bin/ScumBag.real "$@"
EOF
chmod +x /app/bin/scum-bag

# Install desktop files
install -Dm644 com.github.rthomasv3.ScumBag.desktop /app/share/applications/com.github.rthomasv3.ScumBag.desktop
install -Dm644 com.github.rthomasv3.ScumBag.png /app/share/icons/hicolor/128x128/apps/com.github.rthomasv3.ScumBag.png
install -Dm644 com.github.rthomasv3.ScumBag.metainfo.xml /app/share/metainfo/com.github.rthomasv3.ScumBag.metainfo.xml