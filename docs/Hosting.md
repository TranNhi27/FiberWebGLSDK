# Hosting your own build

Unity only scans `Assets/WebGLTemplates/` for templates, **not** `Packages/`. This is a
Unity limitation, not a bug in this SDK: a package can't ship a WebGL template you can
select directly.

## Steps

1. Copy `Samples/PaymentFlowDemo/WebGLTemplate~/` into `Assets/WebGLTemplates/FiberTemplate/`
   in your own project.
2. Select it under **Project Settings → Player → WebGL → Resolution and Presentation → WebGL Template**.
3. Serve the build with these headers (required for `SharedArrayBuffer`, which Fiber's
   WASM node needs):

   ```
   Cross-Origin-Opener-Policy: same-origin
   Cross-Origin-Embedder-Policy: require-corp
   ```

   Unity's "Build and Run" server does **not** send these, so you need your own static
   server or a host (Netlify, Vercel, etc.) set up to add them. To confirm, open the
   browser console on the hosted page and check that `crossOriginIsolated` is `true`.
   Missing headers show up as an immediate `CrossOriginIsolationRequired` error from
   `Initialize`.

4. If players will reach your page from anywhere (not just your local network), the
   peer you connect to must have a `wss` address reachable from the public internet. A
   `127.0.0.1` or LAN address won't work once the page is served over HTTPS, even
   though it connects from `localhost` during local testing. Browsers block insecure
   `ws://` connections from secure pages, and the SDK can't work around that.

## Netlify

Add a file named `_headers` (no extension) next to `index.html` in the folder you deploy:

```
/*
  Cross-Origin-Opener-Policy: same-origin
  Cross-Origin-Embedder-Policy: require-corp
```

Large builds are over the size limit for drag-and-drop deploys, so use the Netlify CLI:
`netlify deploy --prod --dir <build folder>`.