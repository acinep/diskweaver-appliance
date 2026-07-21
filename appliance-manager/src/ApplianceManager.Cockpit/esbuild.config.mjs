import * as esbuild from "esbuild";

const watch = process.argv.includes("--watch");

const options = {
    entryPoints: ["src/app.jsx"],
    bundle: true,
    outdir: "dist",
    format: "iife",
    target: "es2020",
    logLevel: "info",
    minify: !watch,
    sourcemap: watch ? "inline" : false,
    loader: {
        ".jsx": "jsx",
        // Same reasoning as DiskWeaver.Cockpit's build: PatternFly's base.css references its own
        // font/background assets by relative url(). Fonts are emitted as separate hashed files
        // (large, inlining would bloat app.css); the handful of small background SVGs are inlined
        // as data URLs to avoid shipping a bunch of tiny files.
        ".woff2": "file",
        ".svg": "dataurl",
    },
};

if (watch) {
    const ctx = await esbuild.context(options);
    await ctx.watch();
} else {
    await esbuild.build(options);
}
