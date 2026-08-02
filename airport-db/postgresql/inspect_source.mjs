#!/usr/bin/env node

import { createHash } from "node:crypto";
import { createReadStream, promises as fs } from "node:fs";
import { basename, dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { createZstdDecompress } from "node:zlib";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const sourceDirectory = join(scriptDirectory, "..");
const concurrency = Math.max(
  1,
  Math.min(8, Number.parseInt(process.env.AIRPORT_INSPECT_JOBS ?? "4", 10) || 4),
);

function tableNameFromFile(fileName) {
  return fileName
    .replace(/^airportdb@/, "")
    .replace(/(?:@@?\d+)?\.tsv\.zst$/, "");
}

function chunkNumber(fileName) {
  const match = fileName.match(/@{1,2}(\d+)\.tsv\.zst$/);
  return match ? Number.parseInt(match[1], 10) : 0;
}

async function inspectFile(path) {
  const compressedHash = createHash("sha256");
  const input = createReadStream(path);
  const decompressor = createZstdDecompress();
  let rows = 0;
  let uncompressedBytes = 0;
  let lastByte = null;

  input.on("data", (chunk) => compressedHash.update(chunk));
  input.pipe(decompressor);

  for await (const chunk of decompressor) {
    uncompressedBytes += chunk.length;
    for (let index = 0; index < chunk.length; index += 1) {
      if (chunk[index] === 0x0a) {
        rows += 1;
      }
    }
    if (chunk.length > 0) {
      lastByte = chunk[chunk.length - 1];
    }
  }

  if (uncompressedBytes > 0 && lastByte !== 0x0a) {
    rows += 1;
  }

  const stat = await fs.stat(path);
  return {
    file: basename(path),
    table: tableNameFromFile(basename(path)),
    chunk: chunkNumber(basename(path)),
    rows,
    compressedBytes: stat.size,
    uncompressedBytes,
    sha256: compressedHash.digest("hex"),
  };
}

async function mapWithConcurrency(items, worker, limit) {
  const results = new Array(items.length);
  let nextIndex = 0;

  async function runWorker() {
    while (nextIndex < items.length) {
      const index = nextIndex;
      nextIndex += 1;
      results[index] = await worker(items[index]);
      process.stderr.write(
        `[${index + 1}/${items.length}] ${basename(items[index])}: ${results[index].rows.toLocaleString("en-US")} filas\n`,
      );
    }
  }

  await Promise.all(
    Array.from({ length: Math.min(limit, items.length) }, () => runWorker()),
  );
  return results;
}

const fileNames = (await fs.readdir(sourceDirectory))
  .filter((fileName) => fileName.endsWith(".tsv.zst"))
  .sort((left, right) => {
    const tableComparison = tableNameFromFile(left).localeCompare(
      tableNameFromFile(right),
    );
    return tableComparison || chunkNumber(left) - chunkNumber(right);
  });

if (fileNames.length === 0) {
  throw new Error(`No se encontraron archivos .tsv.zst en ${sourceDirectory}`);
}

const files = await mapWithConcurrency(
  fileNames.map((fileName) => join(sourceDirectory, fileName)),
  inspectFile,
  concurrency,
);

const tables = {};
for (const file of files) {
  tables[file.table] ??= {
    rows: 0,
    compressedBytes: 0,
    uncompressedBytes: 0,
    chunks: 0,
  };
  tables[file.table].rows += file.rows;
  tables[file.table].compressedBytes += file.compressedBytes;
  tables[file.table].uncompressedBytes += file.uncompressedBytes;
  tables[file.table].chunks += 1;
}

const result = {
  generatedAt: new Date().toISOString(),
  sourceDirectory,
  fileCount: files.length,
  totals: {
    rows: Object.values(tables).reduce((sum, table) => sum + table.rows, 0),
    compressedBytes: files.reduce((sum, file) => sum + file.compressedBytes, 0),
    uncompressedBytes: files.reduce(
      (sum, file) => sum + file.uncompressedBytes,
      0,
    ),
  },
  tables,
  files,
};

process.stdout.write(`${JSON.stringify(result, null, 2)}\n`);
