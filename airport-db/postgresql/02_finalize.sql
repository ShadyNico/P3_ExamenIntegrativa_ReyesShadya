\set ON_ERROR_STOP on

INSERT INTO airportdb.airport_geo (
  airport_id,
  name,
  city,
  country,
  latitude,
  longitude,
  geolocation
)
SELECT
  airport_id,
  name,
  city,
  country,
  latitude,
  longitude,
  decode(geolocation_base64, 'base64')
FROM airportdb.airport_geo_import_stage;

DROP TABLE airportdb.airport_geo_import_stage;

ALTER TABLE airportdb.airport
  ADD CONSTRAINT airport_pkey PRIMARY KEY (airport_id),
  ADD CONSTRAINT airport_icao_unq UNIQUE (icao);

ALTER TABLE airportdb.airport_geo
  ADD CONSTRAINT airport_geo_pkey PRIMARY KEY (airport_id);

ALTER TABLE airportdb.airport_reachable
  ADD CONSTRAINT airport_reachable_pkey PRIMARY KEY (airport_id);

ALTER TABLE airportdb.airline
  ADD CONSTRAINT airline_pkey PRIMARY KEY (airline_id),
  ADD CONSTRAINT airline_iata_unq UNIQUE (iata);

ALTER TABLE airportdb.airplane_type
  ADD CONSTRAINT airplane_type_pkey PRIMARY KEY (type_id);

ALTER TABLE airportdb.airplane
  ADD CONSTRAINT airplane_pkey PRIMARY KEY (airplane_id);

ALTER TABLE airportdb.flightschedule
  ADD CONSTRAINT flightschedule_pkey PRIMARY KEY (flightno);

ALTER TABLE airportdb.passenger
  ADD CONSTRAINT passenger_pkey PRIMARY KEY (passenger_id),
  ADD CONSTRAINT passenger_passportno_unq UNIQUE (passportno);

ALTER TABLE airportdb.passengerdetails
  ADD CONSTRAINT passengerdetails_pkey PRIMARY KEY (passenger_id);

ALTER TABLE airportdb.employee
  ADD CONSTRAINT employee_pkey PRIMARY KEY (employee_id),
  ADD CONSTRAINT employee_username_unq UNIQUE (username);

ALTER TABLE airportdb.flight
  ADD CONSTRAINT flight_pkey PRIMARY KEY (flight_id);

ALTER TABLE airportdb.flight_log
  ADD CONSTRAINT flight_log_pkey PRIMARY KEY (flight_log_id);

ALTER TABLE airportdb.weatherdata
  ADD CONSTRAINT weatherdata_pkey PRIMARY KEY (log_date, "time", station);

ALTER TABLE airportdb.booking
  ADD CONSTRAINT booking_pkey PRIMARY KEY (booking_id),
  ADD CONSTRAINT booking_seatplan_unq UNIQUE (flight_id, seat);

CREATE INDEX airport_name_idx ON airportdb.airport (name);
CREATE INDEX airport_iata_idx ON airportdb.airport (iata);
CREATE INDEX airline_base_airport_idx ON airportdb.airline (base_airport);
CREATE INDEX airplane_type_id_idx ON airportdb.airplane (type_id);
CREATE INDEX airplane_airline_id_idx ON airportdb.airplane (airline_id);
CREATE INDEX flightschedule_from_idx ON airportdb.flightschedule ("from");
CREATE INDEX flightschedule_to_idx ON airportdb.flightschedule ("to");
CREATE INDEX flightschedule_airline_idx ON airportdb.flightschedule (airline_id);
CREATE INDEX flight_from_idx ON airportdb.flight ("from");
CREATE INDEX flight_to_idx ON airportdb.flight ("to");
CREATE INDEX flight_departure_idx ON airportdb.flight (departure);
CREATE INDEX flight_arrival_idx ON airportdb.flight (arrival);
CREATE INDEX flight_airline_idx ON airportdb.flight (airline_id);
CREATE INDEX flight_airplane_idx ON airportdb.flight (airplane_id);
CREATE INDEX flight_flightno_idx ON airportdb.flight (flightno);
CREATE INDEX flight_log_flight_idx ON airportdb.flight_log (flight_id);
CREATE INDEX booking_passenger_idx ON airportdb.booking (passenger_id);

-- Equivalentes PostgreSQL para los índices FULLTEXT y SPATIAL de MySQL.
CREATE INDEX airplane_type_description_full_idx
  ON airportdb.airplane_type
  USING gin (
    to_tsvector(
      'simple',
      coalesce(identifier, '') || ' ' || coalesce(description, '')
    )
  );

CREATE INDEX airport_geo_point_gist_idx
  ON airportdb.airport_geo
  USING gist (location);

ALTER TABLE airportdb.airport_geo
  ADD CONSTRAINT airport_geo_airport_fk
    FOREIGN KEY (airport_id) REFERENCES airportdb.airport (airport_id);

ALTER TABLE airportdb.airport_reachable
  ADD CONSTRAINT airport_reachable_airport_fk
    FOREIGN KEY (airport_id) REFERENCES airportdb.airport (airport_id);

ALTER TABLE airportdb.airline
  ADD CONSTRAINT airline_base_airport_fk
    FOREIGN KEY (base_airport)
    REFERENCES airportdb.airport (airport_id)
    NOT VALID;

COMMENT ON CONSTRAINT airline_base_airport_fk ON airportdb.airline IS
  'No validada para conservar 3 referencias huérfanas del origen: aerolíneas 15, 51 y 78. Sí se aplica a inserciones y actualizaciones nuevas.';

ALTER TABLE airportdb.airplane
  ADD CONSTRAINT airplane_type_fk
    FOREIGN KEY (type_id) REFERENCES airportdb.airplane_type (type_id),
  ADD CONSTRAINT airplane_airline_fk
    FOREIGN KEY (airline_id) REFERENCES airportdb.airline (airline_id);

ALTER TABLE airportdb.flightschedule
  ADD CONSTRAINT flightschedule_from_fk
    FOREIGN KEY ("from") REFERENCES airportdb.airport (airport_id),
  ADD CONSTRAINT flightschedule_to_fk
    FOREIGN KEY ("to") REFERENCES airportdb.airport (airport_id),
  ADD CONSTRAINT flightschedule_airline_fk
    FOREIGN KEY (airline_id) REFERENCES airportdb.airline (airline_id);

ALTER TABLE airportdb.passengerdetails
  ADD CONSTRAINT passengerdetails_passenger_fk
    FOREIGN KEY (passenger_id)
    REFERENCES airportdb.passenger (passenger_id)
    ON DELETE CASCADE;

ALTER TABLE airportdb.flight
  ADD CONSTRAINT flight_from_fk
    FOREIGN KEY ("from") REFERENCES airportdb.airport (airport_id),
  ADD CONSTRAINT flight_to_fk
    FOREIGN KEY ("to") REFERENCES airportdb.airport (airport_id),
  ADD CONSTRAINT flight_airline_fk
    FOREIGN KEY (airline_id) REFERENCES airportdb.airline (airline_id),
  ADD CONSTRAINT flight_airplane_fk
    FOREIGN KEY (airplane_id) REFERENCES airportdb.airplane (airplane_id),
  ADD CONSTRAINT flight_schedule_fk
    FOREIGN KEY (flightno) REFERENCES airportdb.flightschedule (flightno);

ALTER TABLE airportdb.flight_log
  ADD CONSTRAINT flight_log_flight_fk
    FOREIGN KEY (flight_id) REFERENCES airportdb.flight (flight_id);

ALTER TABLE airportdb.booking
  ADD CONSTRAINT booking_flight_fk
    FOREIGN KEY (flight_id) REFERENCES airportdb.flight (flight_id),
  ADD CONSTRAINT booking_passenger_fk
    FOREIGN KEY (passenger_id) REFERENCES airportdb.passenger (passenger_id);

SELECT setval(
  pg_get_serial_sequence('airportdb.airport', 'airport_id'),
  coalesce(max(airport_id), 1),
  max(airport_id) IS NOT NULL
) FROM airportdb.airport;

SELECT setval(
  pg_get_serial_sequence('airportdb.airline', 'airline_id'),
  coalesce(max(airline_id), 1),
  max(airline_id) IS NOT NULL
) FROM airportdb.airline;

SELECT setval(
  pg_get_serial_sequence('airportdb.airplane_type', 'type_id'),
  coalesce(max(type_id), 1),
  max(type_id) IS NOT NULL
) FROM airportdb.airplane_type;

SELECT setval(
  pg_get_serial_sequence('airportdb.airplane', 'airplane_id'),
  coalesce(max(airplane_id), 1),
  max(airplane_id) IS NOT NULL
) FROM airportdb.airplane;

SELECT setval(
  pg_get_serial_sequence('airportdb.passenger', 'passenger_id'),
  coalesce(max(passenger_id), 1),
  max(passenger_id) IS NOT NULL
) FROM airportdb.passenger;

SELECT setval(
  pg_get_serial_sequence('airportdb.employee', 'employee_id'),
  coalesce(max(employee_id), 1),
  max(employee_id) IS NOT NULL
) FROM airportdb.employee;

SELECT setval(
  pg_get_serial_sequence('airportdb.flight', 'flight_id'),
  coalesce(max(flight_id), 1),
  max(flight_id) IS NOT NULL
) FROM airportdb.flight;

SELECT setval(
  pg_get_serial_sequence('airportdb.flight_log', 'flight_log_id'),
  coalesce(max(flight_log_id), 1),
  max(flight_log_id) IS NOT NULL
) FROM airportdb.flight_log;

SELECT setval(
  pg_get_serial_sequence('airportdb.booking', 'booking_id'),
  coalesce(max(booking_id), 1),
  max(booking_id) IS NOT NULL
) FROM airportdb.booking;

ANALYZE airportdb.airport;
ANALYZE airportdb.airport_geo;
ANALYZE airportdb.airport_reachable;
ANALYZE airportdb.airline;
ANALYZE airportdb.airplane_type;
ANALYZE airportdb.airplane;
ANALYZE airportdb.flightschedule;
ANALYZE airportdb.passenger;
ANALYZE airportdb.passengerdetails;
ANALYZE airportdb.employee;
ANALYZE airportdb.flight;
ANALYZE airportdb.flight_log;
ANALYZE airportdb.weatherdata;
ANALYZE airportdb.booking;
