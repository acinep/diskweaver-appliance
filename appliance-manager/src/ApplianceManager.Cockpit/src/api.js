// appliance-managerd listens on a Unix socket in production; the daemon's own
// APPLIANCEMANAGER_SOCKET env var must point at the same path.
const APPLIANCE_MANAGER_SOCKET = "/run/appliance-managerd.sock";

// `cockpit` is a global provided by ../base1/cockpit.js, loaded via a plain
// <script> tag in index.html before this bundle -- not an ES module, so it's
// read off the global scope rather than imported.
const http = cockpit.http({ unix: APPLIANCE_MANAGER_SOCKET });

// Every call goes through http.request() with a fully-formed path (query string included) rather
// than relying on a params option, since that avoids depending on exactly how cockpit.http()'s
// helper methods build query strings. Same shape as DiskWeaver.Cockpit's api.js.
export function apiRequest(method, path, jsonBody) {
    return http.request({
        method,
        path,
        headers: { "Content-Type": "application/json" },
        body: jsonBody === undefined ? "" : JSON.stringify(jsonBody),
    });
}

export function apiGetJson(path) {
    return apiRequest("GET", path).then(JSON.parse);
}

export function apiPostJson(path, jsonBody) {
    return apiRequest("POST", path, jsonBody).then(JSON.parse);
}

export function apiDeleteJson(path) {
    return apiRequest("DELETE", path).then(JSON.parse);
}
