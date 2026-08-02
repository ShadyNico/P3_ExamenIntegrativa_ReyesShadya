\set ON_ERROR_STOP on

DO $verify$
DECLARE
  expected record;
  actual_rows bigint;
BEGIN
  FOR expected IN
    SELECT *
    FROM (
      VALUES
        ('airline', 113::bigint),
        ('airplane', 5583::bigint),
        ('airplane_type', 342::bigint),
        ('airport', 9854::bigint),
        ('airport_geo', 9854::bigint),
        ('airport_reachable', 0::bigint),
        ('booking', 54304619::bigint),
        ('employee', 1000::bigint),
        ('flight', 462553::bigint),
        ('flight_log', 0::bigint),
        ('flightschedule', 9881::bigint),
        ('passenger', 36095::bigint),
        ('passengerdetails', 36095::bigint),
        ('weatherdata', 4626432::bigint)
    ) AS expected_counts(table_name, expected_rows)
  LOOP
    EXECUTE format(
      'SELECT count(*) FROM airportdb.%I',
      expected.table_name
    ) INTO actual_rows;

    IF actual_rows <> expected.expected_rows THEN
      RAISE EXCEPTION
        'Conteo incorrecto en airportdb.%: esperado %, obtenido %',
        expected.table_name,
        expected.expected_rows,
        actual_rows;
    END IF;

    RAISE NOTICE
      'airportdb.%: % filas (correcto)',
      expected.table_name,
      actual_rows;
  END LOOP;
END
$verify$;

DO $verify_legacy_exceptions$
DECLARE
  orphan_airlines bigint;
BEGIN
  SELECT count(*)
  INTO orphan_airlines
  FROM airportdb.airline AS airline
  WHERE NOT EXISTS (
    SELECT 1
    FROM airportdb.airport AS airport
    WHERE airport.airport_id = airline.base_airport
  );

  IF orphan_airlines <> 3 THEN
    RAISE EXCEPTION
      'Excepciones heredadas inesperadas en airline.base_airport: esperadas 3, obtenidas %',
      orphan_airlines;
  END IF;

  RAISE NOTICE
    'airline.base_airport: 3 referencias huérfanas heredadas y conservadas (correcto)';
END
$verify_legacy_exceptions$;

DO $verify_total$
DECLARE
  total_rows bigint;
BEGIN
  SELECT
    (SELECT count(*) FROM airportdb.airline)
    + (SELECT count(*) FROM airportdb.airplane)
    + (SELECT count(*) FROM airportdb.airplane_type)
    + (SELECT count(*) FROM airportdb.airport)
    + (SELECT count(*) FROM airportdb.airport_geo)
    + (SELECT count(*) FROM airportdb.airport_reachable)
    + (SELECT count(*) FROM airportdb.booking)
    + (SELECT count(*) FROM airportdb.employee)
    + (SELECT count(*) FROM airportdb.flight)
    + (SELECT count(*) FROM airportdb.flight_log)
    + (SELECT count(*) FROM airportdb.flightschedule)
    + (SELECT count(*) FROM airportdb.passenger)
    + (SELECT count(*) FROM airportdb.passengerdetails)
    + (SELECT count(*) FROM airportdb.weatherdata)
  INTO total_rows;

  IF total_rows <> 59502421 THEN
    RAISE EXCEPTION
      'Conteo total incorrecto: esperado 59502421, obtenido %',
      total_rows;
  END IF;

  RAISE NOTICE 'Total: % filas (correcto)', total_rows;
END
$verify_total$;
