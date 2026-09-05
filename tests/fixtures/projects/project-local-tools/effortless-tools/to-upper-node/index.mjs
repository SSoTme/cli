// Node-shape local tool: the CLI hands us the fileset handler's path, so no npm install is needed.
const { serveTool } = await import(process.env.EFFORTLESS_FILESET_HANDLER);

serveTool({
  name: 'to-upper-node',
  transpile: ({ inputFiles, outputName, paramMap, log }) => {
    log(`to-upper-node: ${inputFiles.length} input file(s), PORT=${process.env.PORT}`);
    const first = inputFiles[0];
    if (!first) throw new Error('to-upper-node needs at least one input file.');
    return [
      {
        relativePath: outputName || 'Output.txt',
        contents: first.text.toUpperCase(),
        alwaysOverwrite: true,
      },
      ...(paramMap.extra
        ? [{ relativePath: 'extra.txt', contents: paramMap.extra, alwaysOverwrite: true }]
        : []),
    ];
  },
});
