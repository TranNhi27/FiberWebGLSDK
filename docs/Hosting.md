# Hosting your own build

Unity only scans `Assets/WebGLTemplates/` for templates - **not** `Packages/`. This is a
Unity limitation, not a bug in this SDK: a package cannot ship a directly-usable WebGL
template.

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

   Unity's own "Build and Run" dev server does **not** send these - you need your own
   static server, or a host (Netlify, Vercel, etc.) configured to add them. You can
   confirm isolation is active by opening the browser console on the hosted page and
   checking that `crossOriginIsolated` evaluates to `true`. Missing headers surface as
   an immediate `CrossOriginIsolationRequired` error from `Initialize`.

4. If you're hosting on a domain judges/players will reach from anywhere (not your local
   network), the multiaddr you connect to must be a `wss` address reachable from the
   public internet - a `127.0.0.1` or LAN address will not work once the page itself is
   served over HTTPS, even though it connects fine from `localhost` during local
   testing. This is a browser security restriction (secure pages can't dial insecure
   `ws://` sockets), not something this SDK can work around client-side.
