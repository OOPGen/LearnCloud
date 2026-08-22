#!/bin/sh
# Ensure nginx can write to required dirs as non-root
mkdir -p /tmp/nginx
chmod 755 /tmp/nginx
# Continue with nginx entrypoint
exit 0
