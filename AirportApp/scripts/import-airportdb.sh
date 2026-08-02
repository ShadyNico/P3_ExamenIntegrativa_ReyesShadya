#!/bin/sh
set -eu

marker="$(psql -Atqc "SELECT to_regclass('public.airportdb_import_state') IS NOT NULL")"
if [ "$marker" = "t" ]; then
    completed="$(psql -Atqc "SELECT EXISTS (SELECT 1 FROM public.airportdb_import_state WHERE status = 'complete')")"
    if [ "$completed" = "t" ]; then
        echo "AirportDB ya fue importada; no se repetirá la carga."
        exit 0
    fi
fi

node /data/postgresql/import.mjs --source /data --reset
psql -v ON_ERROR_STOP=1 -c \
    "CREATE TABLE IF NOT EXISTS public.airportdb_import_state (
       status text PRIMARY KEY,
       completed_at timestamptz NOT NULL,
       expected_rows bigint NOT NULL
     );
     INSERT INTO public.airportdb_import_state(status, completed_at, expected_rows)
     VALUES ('complete', CURRENT_TIMESTAMP, 59502421)
     ON CONFLICT (status) DO UPDATE
       SET completed_at = EXCLUDED.completed_at,
           expected_rows = EXCLUDED.expected_rows;"
