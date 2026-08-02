#!/usr/bin/env node

import { spawn } from "node:child_process";
import { createReadStream, promises as fs } from "node:fs";
import { basename, dirname, join, resolve } from "node:path";
import { pipeline } from "node:stream/promises";
import { Transform, Writable } from "node:stream";
import { fileURLToPath } from "node:url";
import * as zlib from "node:zlib";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const inventory = JSON.parse(
  await fs.readFile(join(scriptDirectory, "source_inventory.json"), "utf8"),
);

const tableDefinitions = [
  {
    name: "airport",
    columns: ["airport_id", "iata", "icao", "name"],
  },
  {
    name: "airport_geo",
    target: "airport_geo_import_stage",
    columns: [
      "airport_id",
      "name",
      "city",
      "country",
      "latitude",
      "longitude",
      "geolocation_base64",
    ],
  },
  {
    name: "airport_reachable",
    columns: ["airport_id", "hops"],
  },
  {
    name: "airline",
    columns: ["airline_id", "iata", "airlinename", "base_airport"],
  },
  {
    name: "airplane_type",
    columns: ["type_id", "identifier", "description"],
  },
  {
    name: "airplane",
    columns: ["airplane_id", "capacity", "type_id", "airline_id"],
  },
  {
    name: "flightschedule",
    columns: [
      "flightno",
      "from",
      "to",
      "departure",
      "arrival",
      "airline_id",
      "monday",
      "tuesday",
      "wednesday",
      "thursday",
      "friday",
      "saturday",
      "sunday",
    ],
  },
  {
    name: "passenger",
    columns: ["passenger_id", "passportno", "firstname", "lastname"],
  },
  {
    name: "passengerdetails",
    columns: [
      "passenger_id",
      "birthdate",
      "sex",
      "street",
      "city",
      "zip",
      "country",
      "emailaddress",
      "telephoneno",
    ],
  },
  {
    name: "employee",
    columns: [
      "employee_id",
      "firstname",
      "lastname",
      "birthdate",
      "sex",
      "street",
      "city",
      "zip",
      "country",
      "emailaddress",
      "telephoneno",
      "salary",
      "department",
      "username",
      "password",
    ],
  },
  {
    name: "flight",
    columns: [
      "flight_id",
      "flightno",
      "from",
      "to",
      "departure",
      "arrival",
      "airline_id",
      "airplane_id",
    ],
  },
  {
    name: "flight_log",
    columns: [
      "flight_log_id",
      "log_date",
      "user",
      "flight_id",
      "flightno_old",
      "flightno_new",
      "from_old",
      "to_old",
      "from_new",
      "to_new",
      "departure_old",
      "arrival_old",
      "departure_new",
      "arrival_new",
      "airplane_id_old",
      "airplane_id_new",
      "airline_id_old",
      "airline_id_new",
      "comment",
    ],
  },
  {
    name: "weatherdata",
    columns: [
      "log_date",
      "time",
      "station",
      "temp",
      "humidity",
      "airpressure",
      "wind",
      "weather",
      "winddirection",
    ],
  },
  {
    name: "booking",
    columns: ["booking_id", "flight_id", "seat", "passenger_id", "price"],
  },
];

function usage() {
  return `
Importador completo de AirportDB para PostgreSQL

Uso:
  node import.mjs [opciones]

Opciones:
  -d, --database VALOR  Base de datos o URL aceptada por psql.
  --psql RUTA           Ruta al ejecutable psql (por defecto: psql).
  --source RUTA         Carpeta que contiene los .tsv.zst.
  --reset               Elimina primero el esquema airportdb si existe.
  --check-only          Verifica los 39 archivos sin conectarse a PostgreSQL.
  -h, --help            Muestra esta ayuda.

También se admiten las variables estándar PGHOST, PGPORT, PGDATABASE,
PGUSER y PGPASSWORD. Se recomienda usarlas para no poner contraseñas en
la línea de comandos.
`.trim();
}

function parseArguments(values) {
  const options = {
    database: null,
    psql: "psql",
    source: resolve(scriptDirectory, ".."),
    reset: false,
    checkOnly: false,
  };

  for (let index = 0; index < values.length; index += 1) {
    const value = values[index];
    if (value === "-h" || value === "--help") {
      options.help = true;
    } else if (value === "--reset") {
      options.reset = true;
    } else if (value === "--check-only") {
      options.checkOnly = true;
    } else if (value === "-d" || value === "--database") {
      index += 1;
      if (!values[index]) {
        throw new Error(`${value} requiere un valor`);
      }
      options.database = values[index];
    } else if (value === "--psql") {
      index += 1;
      if (!values[index]) {
        throw new Error("--psql requiere una ruta");
      }
      options.psql = values[index];
    } else if (value === "--source") {
      index += 1;
      if (!values[index]) {
        throw new Error("--source requiere una ruta");
      }
      options.source = resolve(values[index]);
    } else {
      throw new Error(`Opción desconocida: ${value}`);
    }
  }

  if (options.checkOnly && options.reset) {
    throw new Error("--reset no se puede combinar con --check-only");
  }

  return options;
}

function escapeRegularExpression(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function chunkNumber(fileName, tableName) {
  const pattern = new RegExp(
    `^airportdb@${escapeRegularExpression(tableName)}(?:@@?(\\d+))?\\.tsv\\.zst$`,
  );
  const match = fileName.match(pattern);
  return match ? Number.parseInt(match[1] ?? "0", 10) : null;
}

async function resolveSourceFiles(sourceDirectory) {
  const allDataFiles = (await fs.readdir(sourceDirectory)).filter((fileName) =>
    fileName.endsWith(".tsv.zst"),
  );
  const usedFiles = new Set();
  const tableFiles = new Map();

  for (const definition of tableDefinitions) {
    const matches = allDataFiles
      .map((fileName) => ({
        fileName,
        chunk: chunkNumber(fileName, definition.name),
      }))
      .filter((item) => item.chunk !== null)
      .sort((left, right) => left.chunk - right.chunk);

    const expected = inventory.tables[definition.name];
    if (!expected) {
      throw new Error(`No hay inventario para la tabla ${definition.name}`);
    }
    if (matches.length !== expected.chunks) {
      throw new Error(
        `${definition.name}: se esperaban ${expected.chunks} fragmentos y se encontraron ${matches.length}`,
      );
    }

    const distinctChunks = new Set(matches.map((item) => item.chunk));
    if (distinctChunks.size !== matches.length) {
      throw new Error(`${definition.name}: hay números de fragmento duplicados`);
    }

    for (const match of matches) {
      usedFiles.add(match.fileName);
    }
    tableFiles.set(definition.name, matches);
  }

  const unrecognized = allDataFiles.filter((fileName) => !usedFiles.has(fileName));
  if (unrecognized.length > 0) {
    throw new Error(
      `Hay archivos de datos no reconocidos: ${unrecognized.join(", ")}`,
    );
  }
  if (usedFiles.size !== inventory.fileCount) {
    throw new Error(
      `Se esperaban ${inventory.fileCount} archivos y se reconocieron ${usedFiles.size}`,
    );
  }

  return tableFiles;
}

function quoteIdentifier(identifier) {
  return `"${identifier.replaceAll('"', '""')}"`;
}

function copyCommand(definition) {
  const table = quoteIdentifier(definition.target ?? definition.name);
  const columns = definition.columns.map(quoteIdentifier).join(", ");
  return String.raw`\copy "airportdb".${table} (${columns}) FROM STDIN WITH (FORMAT text, DELIMITER E'\t', NULL '\N')`;
}

function psqlBaseArguments(options) {
  const args = ["-X", "--set", "ON_ERROR_STOP=1"];
  if (options.database) {
    args.push("--dbname", options.database);
  }
  return args;
}

function waitForChild(child, description) {
  return new Promise((resolvePromise, rejectPromise) => {
    child.once("error", (error) => {
      rejectPromise(
        new Error(`No se pudo ejecutar ${description}: ${error.message}`),
      );
    });
    child.once("exit", (code, signal) => {
      if (code === 0) {
        resolvePromise();
      } else {
        rejectPromise(
          new Error(
            `${description} terminó con código ${code ?? "desconocido"}${signal ? ` (señal ${signal})` : ""}`,
          ),
        );
      }
    });
  });
}

async function runPsqlFile(options, fileName, singleTransaction = false) {
  const args = psqlBaseArguments(options);
  if (singleTransaction) {
    args.push("--single-transaction");
  }
  args.push("--file", join(scriptDirectory, fileName));

  process.stdout.write(`\nEjecutando ${fileName}...\n`);
  const child = spawn(options.psql, args, {
    stdio: "inherit",
    env: { ...process.env, PGCLIENTENCODING: "UTF8" },
  });
  await waitForChild(child, `psql (${fileName})`);
}

async function inspectOrLoadFile(filePath, destination) {
  const decompressor = zlib.createZstdDecompress();
  let rows = 0;
  let uncompressedBytes = 0;
  let lastByte = null;

  const counter = new Transform({
    transform(chunk, _encoding, callback) {
      uncompressedBytes += chunk.length;
      for (let index = 0; index < chunk.length; index += 1) {
        if (chunk[index] === 0x0a) {
          rows += 1;
        }
      }
      if (chunk.length > 0) {
        lastByte = chunk[chunk.length - 1];
      }
      callback(null, chunk);
    },
  });

  const sink =
    destination ??
    new Writable({
      write(_chunk, _encoding, callback) {
        callback();
      },
    });

  await pipeline(createReadStream(filePath), decompressor, counter, sink);
  if (uncompressedBytes > 0 && lastByte !== 0x0a) {
    rows += 1;
  }

  return { rows, uncompressedBytes };
}

async function loadFile(options, definition, filePath) {
  const args = [
    ...psqlBaseArguments(options),
    "--command",
    copyCommand(definition),
  ];
  const child = spawn(options.psql, args, {
    stdio: ["pipe", "inherit", "inherit"],
    env: { ...process.env, PGCLIENTENCODING: "UTF8" },
  });

  const description = `psql (carga de ${basename(filePath)})`;
  const loadPromise = inspectOrLoadFile(filePath, child.stdin);
  const exitPromise = waitForChild(child, description);
  const [result] = await Promise.all([loadPromise, exitPromise]);
  return result;
}

async function verifyPsql(options) {
  const child = spawn(options.psql, ["--version"], { stdio: "inherit" });
  await waitForChild(child, "psql --version");
}

async function processSource(options, tableFiles, loadIntoDatabase) {
  let totalRows = 0;
  let totalCompressedBytes = 0;
  let totalUncompressedBytes = 0;

  for (const definition of tableDefinitions) {
    const expected = inventory.tables[definition.name];
    const files = tableFiles.get(definition.name);
    let tableRows = 0;
    let tableCompressedBytes = 0;
    let tableUncompressedBytes = 0;

    process.stdout.write(
      `\n${loadIntoDatabase ? "Cargando" : "Verificando"} ${definition.name} (${files.length} fragmento${files.length === 1 ? "" : "s"})...\n`,
    );

    for (const item of files) {
      const filePath = join(options.source, item.fileName);
      const stat = await fs.stat(filePath);
      const result = loadIntoDatabase
        ? await loadFile(options, definition, filePath)
        : await inspectOrLoadFile(filePath);

      tableRows += result.rows;
      tableCompressedBytes += stat.size;
      tableUncompressedBytes += result.uncompressedBytes;
      process.stdout.write(
        `  ${item.fileName}: ${result.rows.toLocaleString("en-US")} filas\n`,
      );
    }

    if (
      tableRows !== expected.rows ||
      tableCompressedBytes !== expected.compressedBytes ||
      tableUncompressedBytes !== expected.uncompressedBytes
    ) {
      throw new Error(
        `${definition.name}: el contenido no coincide con el inventario ` +
          `(filas ${tableRows}/${expected.rows}, comprimido ` +
          `${tableCompressedBytes}/${expected.compressedBytes}, descomprimido ` +
          `${tableUncompressedBytes}/${expected.uncompressedBytes})`,
      );
    }

    totalRows += tableRows;
    totalCompressedBytes += tableCompressedBytes;
    totalUncompressedBytes += tableUncompressedBytes;
  }

  if (
    totalRows !== inventory.totalRows ||
    totalCompressedBytes !== inventory.compressedBytes ||
    totalUncompressedBytes !== inventory.uncompressedBytes
  ) {
    throw new Error("Los totales de origen no coinciden con el inventario");
  }

  process.stdout.write(
    `\nOrigen completo: ${totalRows.toLocaleString("en-US")} filas en ${inventory.fileCount} archivos.\n`,
  );
}

async function main() {
  const options = parseArguments(process.argv.slice(2));
  if (options.help) {
    process.stdout.write(`${usage()}\n`);
    return;
  }
  if (typeof zlib.createZstdDecompress !== "function") {
    throw new Error(
      "Esta versión de Node.js no soporta Zstandard. Use Node.js 22.15 o posterior.",
    );
  }

  const tableFiles = await resolveSourceFiles(options.source);
  if (options.checkOnly) {
    await processSource(options, tableFiles, false);
    process.stdout.write("Verificación del origen finalizada correctamente.\n");
    return;
  }

  await verifyPsql(options);
  if (options.reset) {
    await runPsqlFile(options, "00_reset.sql", true);
  }
  await runPsqlFile(options, "01_schema.sql", true);
  await processSource(options, tableFiles, true);
  await runPsqlFile(options, "02_finalize.sql");
  await runPsqlFile(options, "03_verify.sql");

  process.stdout.write(
    "\nMigración finalizada: esquema airportdb listo para la aplicación web.\n",
  );
}

main().catch((error) => {
  process.stderr.write(`\nERROR: ${error.message}\n`);
  process.exitCode = 1;
});
