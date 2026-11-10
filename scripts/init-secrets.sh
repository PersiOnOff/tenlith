#!/bin/sh
set -eu
umask 077
for name in postgres_password jwt_key; do
  if [ ! -s "/run/tenlith/$name" ]; then
    head -c 48 /dev/urandom | base64 | tr -d '\n' > "/run/tenlith/$name.tmp"
    mv "/run/tenlith/$name.tmp" "/run/tenlith/$name"
  fi
  # Files are shared only with the database/API containers, under distinct UIDs.
  chmod 444 "/run/tenlith/$name"
done
