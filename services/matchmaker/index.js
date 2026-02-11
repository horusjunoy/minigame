const http = require("http");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { URL } = require("url");

const port = parseInt(process.env.MATCHMAKER_PORT || "8080", 10);
const secret = process.env.MATCHMAKER_SECRET || "dev_secret_change_me";
const defaultEndpoint = process.env.MATCHMAKER_DEFAULT_ENDPOINT || "127.0.0.1:7770";
const tokenTtlSeconds = parseInt(process.env.MATCHMAKER_TOKEN_TTL_S || "300", 10);
const heartbeatTtlSeconds = parseInt(process.env.MATCHMAKER_HEARTBEAT_TTL_S || "60", 10);
const maxMatches = parseInt(process.env.MATCHMAKER_MAX_MATCHES || "200", 10);

const matches = new Map();
const startedAt = Date.now();
const buildVersion = process.env.BUILD_VERSION || readBuildVersion();
const alertErrorThreshold = parseInt(process.env.MATCHMAKER_ERROR_ALERT_THRESHOLD || "5", 10);
const alertWindowMs = parseInt(process.env.MATCHMAKER_ALERT_WINDOW_MS || "60000", 10);

const metrics = {
  requestsTotal: 0,
  requestDurationMsTotal: 0,
  requestDurationMsMax: 0,
  errorsTotal: 0,
  errorsWindow: [],
  zombiesTotal: 0,
  zombiesWindow: [],
  matchesCreated: 0,
  lastErrorAlertAt: 0,
  lastZombieAlertAt: 0,
};

const serverPool = parseServerPool(process.env.MATCHMAKER_SERVER_POOL, defaultEndpoint);

function log(event, fields) {
  const payload = {
    ts: new Date().toISOString(),
    lvl: "INFO",
    event,
    build_version: buildVersion,
    service: "matchmaker",
    ...fields,
  };
  console.log(JSON.stringify(payload));
}

function logError(event, fields) {
  const payload = {
    ts: new Date().toISOString(),
    lvl: "ERROR",
    event,
    build_version: buildVersion,
    service: "matchmaker",
    ...fields,
  };
  console.error(JSON.stringify(payload));
  recordError(event);
}

function json(res, statusCode, body) {
  const payload = JSON.stringify(body ?? {});
  res.writeHead(statusCode, {
    "Content-Type": "application/json",
    "Content-Length": Buffer.byteLength(payload),
  });
  res.end(payload);
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    let data = "";
    req.on("data", chunk => {
      data += chunk;
    });
    req.on("end", () => resolve(data));
    req.on("error", reject);
  });
}

function base64UrlEncode(input) {
  return Buffer.from(input).toString("base64url");
}

function base64UrlDecode(input) {
  return Buffer.from(input, "base64url").toString("utf8");
}

function signToken(payload) {
  const body = JSON.stringify(payload);
  const signature = crypto.createHmac("sha256", secret).update(body).digest("base64url");
  return `${base64UrlEncode(body)}.${signature}`;
}

function verifyToken(token) {
  if (!token || typeof token !== "string") return null;
  const [encoded, signature] = token.split(".");
  if (!encoded || !signature) return null;
  const body = base64UrlDecode(encoded);
  const expected = crypto.createHmac("sha256", secret).update(body).digest("base64url");
  if (!crypto.timingSafeEqual(Buffer.from(signature), Buffer.from(expected))) return null;
  const payload = JSON.parse(body);
  if (payload.exp && Date.now() > payload.exp) return null;
  return payload;
}

function generateMatchId() {
  const suffix = crypto.randomBytes(4).toString("hex");
  return `m_${Date.now()}_${suffix}`;
}

function generatePlayerId() {
  return `p_${crypto.randomBytes(3).toString("hex")}`;
}

function createMatch({ minigame_id, max_players }) {
  const matchId = generateMatchId();
  const allocation = allocateServer();
  if (!allocation) {
    return null;
  }

  metrics.matchesCreated += 1;
  const match = {
    match_id: matchId,
    minigame_id: minigame_id || "stub_v1",
    status: "waiting",
    created_at: new Date().toISOString(),
    max_players: max_players || 16,
    players: 0,
    endpoint: allocation.endpoint,
    server_endpoint: allocation.endpoint,
    last_heartbeat: Date.now(),
  };
  matches.set(matchId, match);
  allocation.activeMatches += 1;
  return match;
}

function cleanupZombies() {
  const now = Date.now();
  for (const [matchId, match] of matches.entries()) {
    if (match.status === "ended") continue;
    const age = now - match.last_heartbeat;
    if (age > heartbeatTtlSeconds * 1000) {
      log("match_zombie", { match_id: matchId, age_ms: age });
      recordZombie(matchId);
      releaseMatch(match);
      matches.delete(matchId);
    }
  }
}

setInterval(cleanupZombies, 5000).unref();

const server = http.createServer(async (req, res) => {
  const requestStart = process.hrtime.bigint();
  try {
    const url = new URL(req.url, `http://${req.headers.host}`);
    const path = url.pathname;
    const method = req.method?.toUpperCase() || "GET";

    if (method === "GET" && path === "/health") {
      const uptime = Math.floor((Date.now() - startedAt) / 1000);
      return respond(res, requestStart, 200, {
        status: "ok",
        uptime_s: uptime,
        matches: matches.size,
        build_version: buildVersion,
      });
    }

    if (method === "POST" && path === "/matches") {
      const body = await readBody(req);
      const payload = body ? JSON.parse(body) : {};
      if (countActiveMatches() >= maxMatches) {
        log("match_allocation_failed", { reason: "capacity", max_matches: maxMatches });
        return respond(res, requestStart, 503, { error: "allocation_failed" });
      }
      const match = createMatch(payload);
      if (!match) {
        log("match_allocation_failed", { reason: "server_pool_exhausted" });
        return respond(res, requestStart, 503, { error: "allocation_failed" });
      }
      const hostToken = signToken({
        match_id: match.match_id,
        player_id: "host",
        exp: Date.now() + tokenTtlSeconds * 1000,
      });
      log("match_created", { match_id: match.match_id, minigame_id: match.minigame_id });
      return respond(res, requestStart, 200, {
        match_id: match.match_id,
        endpoint: match.endpoint,
        join_token: hostToken,
        status: match.status,
      });
    }

    if (method === "GET" && path === "/matches") {
      const minigameId = url.searchParams.get("minigame_id");
      const list = [];
      for (const match of matches.values()) {
        if (minigameId && match.minigame_id !== minigameId) continue;
        if (match.status === "ended") continue;
        list.push({
          match_id: match.match_id,
          players: match.players,
          max_players: match.max_players,
          status: match.status,
          minigame_id: match.minigame_id,
          endpoint: match.endpoint,
        });
      }
      return respond(res, requestStart, 200, list);
    }

    const matchJoin = path.match(/^\/matches\/([^/]+)\/join$/);
    if (method === "POST" && matchJoin) {
      const matchId = matchJoin[1];
      const match = matches.get(matchId);
      if (!match) return respond(res, requestStart, 404, { error: "match_not_found" });
      if (match.status === "ended") return respond(res, requestStart, 410, { error: "match_ended" });
      if (match.players >= match.max_players) return respond(res, requestStart, 409, { error: "match_full" });
      const playerId = generatePlayerId();
      match.players += 1;
      incrementServerPlayers(match);
      const joinToken = signToken({
        match_id: match.match_id,
        player_id: playerId,
        exp: Date.now() + tokenTtlSeconds * 1000,
      });
      log("match_join", { match_id: match.match_id, player_id: playerId });
      return respond(res, requestStart, 200, { endpoint: match.endpoint, join_token: joinToken });
    }

    const matchEnd = path.match(/^\/matches\/([^/]+)\/end$/);
    if (method === "POST" && matchEnd) {
      const matchId = matchEnd[1];
      const match = matches.get(matchId);
      if (!match) return respond(res, requestStart, 404, { error: "match_not_found" });
      const body = await readBody(req);
      const payload = body ? JSON.parse(body) : {};
      match.status = "ended";
      match.ended_at = new Date().toISOString();
      match.end_reason = payload.reason || "unknown";
      releaseMatch(match);
      log("match_ended", { match_id: matchId, reason: match.end_reason });
      return respond(res, requestStart, 200, { status: "ok" });
    }

    const matchHeartbeat = path.match(/^\/matches\/([^/]+)\/heartbeat$/);
    if (method === "POST" && matchHeartbeat) {
      const matchId = matchHeartbeat[1];
      const match = matches.get(matchId);
      if (!match) return respond(res, requestStart, 404, { error: "match_not_found" });
      match.last_heartbeat = Date.now();
      if (match.status === "waiting") match.status = "active";
      log("match_heartbeat", { match_id: matchId });
      return respond(res, requestStart, 200, { status: "ok" });
    }

    if (method === "POST" && path === "/tokens/verify") {
      const body = await readBody(req);
      const payload = body ? JSON.parse(body) : {};
      const tokenPayload = verifyToken(payload.token);
      if (!tokenPayload) return respond(res, requestStart, 401, { error: "invalid_token" });
      return respond(res, requestStart, 200, tokenPayload);
    }

    if (method === "GET" && path === "/metrics") {
      return respondText(res, requestStart, 200, buildMetrics());
    }

    if (method === "GET" && path === "/dashboard") {
      return respondText(res, requestStart, 200, buildDashboard());
    }

    return respond(res, requestStart, 404, { error: "not_found" });
  } catch (err) {
    logError("matchmaker_error", { message: err.message });
    return respond(res, requestStart, 500, { error: "server_error" });
  }
});

server.listen(port, () => {
  log("matchmaker_started", { port, endpoint: defaultEndpoint });
});

function readBuildVersion() {
  try {
    const versionPath = path.join(__dirname, "..", "..", "build_version.txt");
    const value = fs.readFileSync(versionPath, "utf8").trim();
    return value || "unknown";
  } catch {
    return "unknown";
  }
}

function respond(res, start, statusCode, body) {
  recordRequest(start, statusCode);
  return json(res, statusCode, body);
}

function respondText(res, start, statusCode, body) {
  recordRequest(start, statusCode);
  res.writeHead(statusCode, {
    "Content-Type": "text/plain; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

function recordRequest(start, statusCode) {
  const durationMs = Number(process.hrtime.bigint() - start) / 1e6;
  metrics.requestsTotal += 1;
  metrics.requestDurationMsTotal += durationMs;
  metrics.requestDurationMsMax = Math.max(metrics.requestDurationMsMax, durationMs);
  if (statusCode >= 500) {
    recordError("http_5xx");
  }
}

function recordError(reason) {
  const now = Date.now();
  metrics.errorsTotal += 1;
  metrics.errorsWindow.push(now);
  pruneWindow(metrics.errorsWindow, now);
  if (metrics.errorsWindow.length >= alertErrorThreshold && now - metrics.lastErrorAlertAt > alertWindowMs) {
    metrics.lastErrorAlertAt = now;
    log("alert_critical_error_rate", { errors_last_window: metrics.errorsWindow.length, window_ms: alertWindowMs, reason });
  }
}

function recordZombie(matchId) {
  const now = Date.now();
  metrics.zombiesTotal += 1;
  metrics.zombiesWindow.push(now);
  pruneWindow(metrics.zombiesWindow, now);
  if (now - metrics.lastZombieAlertAt > alertWindowMs) {
    metrics.lastZombieAlertAt = now;
    log("alert_server_down", { match_id: matchId });
  }
}

function pruneWindow(window, now) {
  while (window.length > 0 && now - window[0] > alertWindowMs) {
    window.shift();
  }
}

function countActiveMatches() {
  let active = 0;
  for (const match of matches.values()) {
    if (match.status !== "ended") {
      active += 1;
    }
  }
  return active;
}

function buildMetrics() {
  const now = Date.now();
  pruneWindow(metrics.errorsWindow, now);
  pruneWindow(metrics.zombiesWindow, now);
  const avgLatency = metrics.requestsTotal > 0 ? metrics.requestDurationMsTotal / metrics.requestsTotal : 0;
  const activeMatches = countActiveMatches();
  const lines = [
    "# HELP matchmaker_matches_active Active matches in memory",
    "# TYPE matchmaker_matches_active gauge",
    `matchmaker_matches_active ${activeMatches}`,
    "# HELP matchmaker_matches_created_total Matches created",
    "# TYPE matchmaker_matches_created_total counter",
    `matchmaker_matches_created_total ${metrics.matchesCreated}`,
    "# HELP matchmaker_request_duration_ms_sum Total request duration in ms",
    "# TYPE matchmaker_request_duration_ms_sum counter",
    `matchmaker_request_duration_ms_sum ${metrics.requestDurationMsTotal.toFixed(2)}`,
    "# HELP matchmaker_request_duration_ms_count Request count",
    "# TYPE matchmaker_request_duration_ms_count counter",
    `matchmaker_request_duration_ms_count ${metrics.requestsTotal}`,
    "# HELP matchmaker_request_duration_ms_max Max request duration in ms",
    "# TYPE matchmaker_request_duration_ms_max gauge",
    `matchmaker_request_duration_ms_max ${metrics.requestDurationMsMax.toFixed(2)}`,
    "# HELP matchmaker_latency_ms_avg Average request latency in ms",
    "# TYPE matchmaker_latency_ms_avg gauge",
    `matchmaker_latency_ms_avg ${avgLatency.toFixed(2)}`,
    "# HELP matchmaker_errors_total Total errors",
    "# TYPE matchmaker_errors_total counter",
    `matchmaker_errors_total ${metrics.errorsTotal}`,
    "# HELP matchmaker_errors_window Errors in alert window",
    "# TYPE matchmaker_errors_window gauge",
    `matchmaker_errors_window ${metrics.errorsWindow.length}`,
    "# HELP matchmaker_zombies_total Total zombie matches",
    "# TYPE matchmaker_zombies_total counter",
    `matchmaker_zombies_total ${metrics.zombiesTotal}`,
    "# HELP matchmaker_crash_rate_per_min Zombie matches in window",
    "# TYPE matchmaker_crash_rate_per_min gauge",
    `matchmaker_crash_rate_per_min ${metrics.zombiesWindow.length}`,
  ];

  for (const server of serverPool) {
    const label = `endpoint="${server.endpoint}"`;
    const loadRatio = server.capacity > 0 ? server.activeMatches / server.capacity : 0;
    lines.push("# HELP matchmaker_server_capacity Server capacity in matches");
    lines.push("# TYPE matchmaker_server_capacity gauge");
    lines.push(`matchmaker_server_capacity{${label}} ${server.capacity}`);
    lines.push("# HELP matchmaker_server_active_matches Active matches per server");
    lines.push("# TYPE matchmaker_server_active_matches gauge");
    lines.push(`matchmaker_server_active_matches{${label}} ${server.activeMatches}`);
    lines.push("# HELP matchmaker_server_active_players Active players per server");
    lines.push("# TYPE matchmaker_server_active_players gauge");
    lines.push(`matchmaker_server_active_players{${label}} ${server.activePlayers}`);
    lines.push("# HELP matchmaker_server_load_ratio Active matches / capacity");
    lines.push("# TYPE matchmaker_server_load_ratio gauge");
    lines.push(`matchmaker_server_load_ratio{${label}} ${loadRatio.toFixed(2)}`);
  }

  return lines.join("\n") + "\n";
}

function buildDashboard() {
  const now = Date.now();
  pruneWindow(metrics.errorsWindow, now);
  pruneWindow(metrics.zombiesWindow, now);
  const avgLatency = metrics.requestsTotal > 0 ? metrics.requestDurationMsTotal / metrics.requestsTotal : 0;
  const activeMatches = countActiveMatches();
  const crashRate = metrics.zombiesWindow.length;
  const serverLines = serverPool.map(server => {
    const loadRatio = server.capacity > 0 ? server.activeMatches / server.capacity : 0;
    return `server=${server.endpoint} matches=${server.activeMatches}/${server.capacity} players=${server.activePlayers} load=${loadRatio.toFixed(2)}`;
  });
  return [
    "Mini-game Platform Dashboard",
    `build_version=${buildVersion}`,
    `matches_active=${activeMatches}`,
    `crash_rate_per_min=${crashRate}`,
    `latency_ms_avg=${avgLatency.toFixed(2)}`,
    `errors_last_window=${metrics.errorsWindow.length}`,
    ...serverLines,
  ].join("\n") + "\n";
}

function parseServerPool(raw, fallbackEndpoint) {
  const entries = [];
  const defaultCapacity = 4;
  if (raw && typeof raw === "string") {
    const parts = raw.split(/[;,]/).map(item => item.trim()).filter(Boolean);
    for (const part of parts) {
      const [endpoint, capacityText] = part.split("=");
      if (!endpoint) continue;
      const capacity = parseInt(capacityText || `${defaultCapacity}`, 10);
      entries.push({
        endpoint: endpoint.trim(),
        capacity: Number.isFinite(capacity) && capacity > 0 ? capacity : defaultCapacity,
        activeMatches: 0,
        activePlayers: 0,
      });
    }
  }

  if (entries.length === 0) {
    entries.push({
      endpoint: fallbackEndpoint,
      capacity: defaultCapacity,
      activeMatches: 0,
      activePlayers: 0,
    });
  }

  return entries;
}

function allocateServer() {
  if (!serverPool.length) return null;
  const sorted = [...serverPool].sort((a, b) => {
    const loadA = a.capacity > 0 ? a.activeMatches / a.capacity : 1;
    const loadB = b.capacity > 0 ? b.activeMatches / b.capacity : 1;
    if (loadA !== loadB) return loadA - loadB;
    return a.activeMatches - b.activeMatches;
  });

  for (const server of sorted) {
    if (server.activeMatches < server.capacity) {
      return server;
    }
  }

  return null;
}

function getServerByEndpoint(endpoint) {
  return serverPool.find(server => server.endpoint === endpoint);
}

function incrementServerPlayers(match) {
  if (!match || !match.server_endpoint) return;
  const server = getServerByEndpoint(match.server_endpoint);
  if (!server) return;
  server.activePlayers += 1;
}

function releaseMatch(match) {
  if (!match || !match.server_endpoint) return;
  const server = getServerByEndpoint(match.server_endpoint);
  if (!server) return;
  server.activeMatches = Math.max(0, server.activeMatches - 1);
  if (match.players) {
    server.activePlayers = Math.max(0, server.activePlayers - match.players);
  }
}
