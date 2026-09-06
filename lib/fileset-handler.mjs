// Effortless local-tool fileset handler (step 12 / D30).
//
// Zero-dependency node module that speaks the transpiler wire contract the
// effortless CLI uses for every published tool: a TranspilePayload comes in
// (POST /), a response with Transpiler + TranspileRequest.ZippedOutputFileSet
// + Logs (+ Exception) goes out. A node local tool implements only
// `transpile({ inputFiles, params, paramMap, outputName, toolName, payload })`
// and returns its output files.
//
// The CLI hands a node tool the absolute path of this file in
// EFFORTLESS_FILESET_HANDLER, so a tool needs no npm install:
//
//   const { serveTool } = await import(process.env.EFFORTLESS_FILESET_HANDLER);
//   serveTool({ transpile: ({ inputFiles, outputName }) => [
//     { relativePath: outputName || 'Output.txt', contents: inputFiles[0].text.toUpperCase(), alwaysOverwrite: true },
//   ] });
//
// `createRequestListener` returns a plain (req, res) handler, so the same
// logic can be mounted in express or any other node HTTP framework.

import http from 'node:http';
import zlib from 'node:zlib';

const XML_ESCAPES = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', '\r': '&#xD;' };

export function escapeXml(text) {
  return String(text).replace(/[&<>"\r]/g, (c) => XML_ESCAPES[c]);
}

export function unescapeXml(text) {
  return String(text)
    .replace(/&#x([0-9a-fA-F]+);/g, (_, hex) => String.fromCodePoint(parseInt(hex, 16)))
    .replace(/&#([0-9]+);/g, (_, dec) => String.fromCodePoint(parseInt(dec, 10)))
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, '&');
}

function unwrapCdata(text) {
  const trimmed = text.trim();
  if (trimmed.startsWith('<![CDATA[') && trimmed.endsWith(']]>')) {
    return trimmed.slice('<![CDATA['.length, -']]>'.length);
  }
  return unescapeXml(text);
}

function childText(block, element) {
  const match = new RegExp(`<${element}(?:\\s[^>]*)?>([\\s\\S]*?)</${element}>`).exec(block);
  return match ? match[1] : null;
}

/** Parses FileSet XML into [{ relativePath, contents: Buffer, text, isBinary }]. */
export function parseFileSetXml(xml) {
  const files = [];
  if (!xml) return files;
  const blocks = String(xml).match(/<FileSetFile(?:\s[^>]*)?>[\s\S]*?<\/FileSetFile>/g) || [];
  for (const block of blocks) {
    const relativePath = childText(block, 'RelativePath');
    if (!relativePath) continue;
    let contents;
    let isBinary = false;
    const plain = childText(block, 'FileContents');
    const zippedText = childText(block, 'ZippedTextFileContents') ?? childText(block, 'ZippedFileContents');
    const zippedBinary = childText(block, 'ZippedBinaryFileContents');
    const binary = childText(block, 'BinaryFileContents');
    if (plain !== null) {
      contents = Buffer.from(unwrapCdata(plain), 'utf8');
    } else if (zippedText !== null) {
      contents = zlib.gunzipSync(Buffer.from(zippedText.trim(), 'base64'));
    } else if (zippedBinary !== null) {
      contents = zlib.gunzipSync(Buffer.from(zippedBinary.trim(), 'base64'));
      isBinary = true;
    } else if (binary !== null) {
      contents = Buffer.from(binary.trim(), 'base64');
      isBinary = true;
    } else {
      contents = Buffer.alloc(0);
    }
    files.push({
      relativePath: unescapeXml(relativePath).replace(/^[\\/]+/, ''),
      contents,
      isBinary,
      get text() {
        return contents.toString('utf8');
      },
    });
  }
  return files;
}

function isXmlSafeText(text) {
  // XML 1.0 cannot carry these control characters even escaped.
  return !/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/.test(text) && !text.includes('[$$NEWUUID$$]');
}

/**
 * Builds FileSet XML from [{ relativePath, contents, alwaysOverwrite, overwriteMode, skipClean }].
 * `contents` may be a string (text) or a Buffer/Uint8Array (binary).
 */
export function buildFileSetXml(files) {
  const parts = ['<?xml version="1.0" encoding="utf-8"?>', '<FileSet>', '  <FileSetFiles>'];
  for (const file of files || []) {
    if (!file || !file.relativePath) {
      throw new Error('Every output file needs a relativePath.');
    }
    const relativePath = String(file.relativePath).replace(/\\/g, '/').replace(/^\/+/, '');
    parts.push('    <FileSetFile>');
    parts.push(`      <RelativePath>${escapeXml(relativePath)}</RelativePath>`);
    const contents = file.contents ?? '';
    if (typeof contents === 'string' && isXmlSafeText(contents)) {
      parts.push(`      <FileContents>${escapeXml(contents)}</FileContents>`);
    } else {
      const bytes = typeof contents === 'string' ? Buffer.from(contents, 'utf8') : Buffer.from(contents);
      parts.push(`      <ZippedBinaryFileContents>${zlib.gzipSync(bytes).toString('base64')}</ZippedBinaryFileContents>`);
    }
    if (file.alwaysOverwrite !== undefined) {
      parts.push(`      <AlwaysOverwrite>${file.alwaysOverwrite ? 'true' : 'false'}</AlwaysOverwrite>`);
    }
    if (file.overwriteMode) {
      parts.push(`      <OverwriteMode>${escapeXml(file.overwriteMode)}</OverwriteMode>`);
    }
    if (file.skipClean !== undefined) {
      parts.push(`      <SkipClean>${file.skipClean ? 'true' : 'false'}</SkipClean>`);
    }
    parts.push('    </FileSetFile>');
  }
  parts.push('  </FileSetFiles>', '</FileSet>', '');
  return parts.join('\n');
}

function prop(obj, name) {
  if (!obj || typeof obj !== 'object') return undefined;
  const key = Object.keys(obj).find((k) => k.toLowerCase() === name.toLowerCase());
  return key === undefined ? undefined : obj[key];
}

/** Decodes a TranspilePayload JSON string into what a tool needs. */
export function decodePayload(json) {
  const payload = typeof json === 'string' ? JSON.parse(json) : json;
  let xml = prop(payload, 'cliInputFileSetXml');
  const request = prop(payload, 'transpileRequest');
  const zipped = prop(request, 'zippedInputFileSet');
  if (!xml && zipped) {
    xml = zlib.gunzipSync(Buffer.from(zipped, 'base64')).toString('utf8');
  }
  const params = prop(payload, 'cliParams') || [];
  const paramMap = {};
  for (const entry of params) {
    const i = String(entry).indexOf('=');
    if (i < 0) paramMap[entry] = '';
    else paramMap[entry.slice(0, i)] = entry.slice(i + 1);
  }
  return {
    payload,
    inputFiles: parseFileSetXml(xml),
    params,
    paramMap,
    outputName: prop(payload, 'cliOutput') || '',
    toolName: prop(payload, 'cliTranspiler') || process.env.EFFORTLESS_TOOL_NAME || '',
    debug: Boolean(prop(payload, 'cliDebug')),
  };
}

/** Shapes the wire response. `error` (string) marks the run as failed. */
export function buildResponse({ name, files = [], logs = [], error = null }) {
  const xml = buildFileSetXml(files);
  return {
    TranspileRequest: { ZippedOutputFileSet: error ? null : zlib.gzipSync(Buffer.from(xml, 'utf8')).toString('base64') },
    Transpiler: { Name: name, LowerHyphenName: name },
    Logs: logs.map((log) =>
      typeof log === 'string'
        ? { Level: 'message', Text: log, Timestamp: new Date().toISOString() }
        : { Level: log.level || log.Level || 'message', Text: log.text || log.Text || '', Timestamp: new Date().toISOString() },
    ),
    Exception: error ? { Message: String(error) } : null,
  };
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on('data', (chunk) => chunks.push(chunk));
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')));
    req.on('error', reject);
  });
}

function sendJson(res, status, value) {
  const body = JSON.stringify(value);
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'Content-Length': Buffer.byteLength(body) });
  res.end(body);
}

/**
 * Returns a (req, res) listener: GET / answers a health object, POST / runs
 * `transpile` and answers the wire response. Mount it anywhere.
 */
export function createRequestListener({ name = process.env.EFFORTLESS_TOOL_NAME || 'local-tool', transpile }) {
  if (typeof transpile !== 'function') {
    throw new Error('createRequestListener needs a transpile({ inputFiles, params, paramMap, outputName }) function.');
  }
  return async (req, res) => {
    const path = (req.url || '/').split('?')[0];
    if (req.method === 'GET') {
      sendJson(res, 200, { status: 'healthy', tool: name });
      return;
    }
    if (req.method !== 'POST' || (path !== '/' && path !== '')) {
      sendJson(res, 404, { error: 'route not found' });
      return;
    }
    const logs = [];
    const log = (text, level = 'message') => logs.push({ level, text });
    try {
      const decoded = decodePayload(await readBody(req));
      const result = await transpile({ ...decoded, log });
      const files = Array.isArray(result) ? result : result?.files || [];
      sendJson(res, 200, buildResponse({ name, files, logs }));
    } catch (error) {
      sendJson(res, 200, buildResponse({ name, logs, error: error?.message || String(error) }));
    }
  };
}

/** Starts an HTTP server on PORT (or `port`) for one tool; returns the server. */
export function serveTool({ name, transpile, port = process.env.PORT || process.env.EFFORTLESS_TOOL_PORT || 0, host = '127.0.0.1' }) {
  const toolName = name || process.env.EFFORTLESS_TOOL_NAME || 'local-tool';
  const server = http.createServer(createRequestListener({ name: toolName, transpile }));
  server.listen(Number(port), host, () => {
    const address = server.address();
    console.log(`[${toolName}] listening on http://${host}:${address.port}/`);
  });
  return server;
}
