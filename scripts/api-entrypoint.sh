#!/bin/sh
set -eu
if [ -f /run/tenlith/postgres_password ]; then
  password="$(cat /run/tenlith/postgres_password)"
  export ConnectionStrings__Database="Host=db;Port=5432;Database=tenlith;Username=tenlith;Password=$password"
fi
if [ -f /run/tenlith/jwt_key ]; then
  export Jwt__Key="$(cat /run/tenlith/jwt_key)"
fi
exec dotnet Tenlith.Api.dll "$@"
