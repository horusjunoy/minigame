const http = require("http");
const crypto = require("crypto");
const fs = require("fs");
const path = require("path");
const { spawn } = require("child_process");
const { URL } = require("url");

const port = parseInt(process.env.MATCHMAKER_PORT || "8080", 10);
const secret = process.env.MATCHMAKER_SECRET || "dev_secret_change_me";
const defaultEndpoint = process.env.MATCHMAKER_DEFAULT_ENDPOINT || "127.0.0.1:7770";
const tokenTtlSeconds = parseInt(process.env.MATCHMAKER_TOKEN_TTL_S || "300", 10);
const heartbeatTtlSeconds = parseInt(process.env.MATCHMAKER_HEARTBEAT_TTL_S || "60", 10);
const maxMatches = parseInt(process.env.MATCHMAKER_MAX_MATCHES || "200", 10);
const rateLimitWindowMs = parseInt(process.env.MATCHMAKER_RATE_LIMIT_WINDOW_MS || "60000", 10);
const rateLimitMax = parseInt(process.env.MATCHMAKER_RATE_LIMIT_MAX || "120", 10);
const crashRateThreshold = parseInt(process.env.MATCHMAKER_CRASH_RATE_THRESHOLD || "3", 10);
const configPath = process.env.MATCHMAKER_CONFIG_PATH || path.join(__dirname, "remote_config.json");
const configCacheTtlMs = parseInt(process.env.MATCHMAKER_CONFIG_CACHE_MS || "5000", 10);
let configCache = null;
let configCacheAt = 0;

const supervisorEnabled = process.env.MATCHMAKER_HOST_SUPERVISOR === "1";
const hostAddress = process.env.MATCHMAKER_HOST_ADDRESS || "127.0.0.1";
const hostBasePort = parseInt(process.env.MATCHMAKER_HOST_BASE_PORT || "7770", 10);
const hostMaxRooms = parseInt(process.env.MATCHMAKER_HOST_MAX_ROOMS || "4", 10);
const hostRestartMax = parseInt(process.env.MATCHMAKER_HOST_RESTART_MAX || "2", 10);
const hostRestartBackoffMs = parseInt(process.env.MATCHMAKER_HOST_RESTART_BACKOFF_MS || "1000", 10);
const hostServerCmd = process.env.MATCHMAKER_SERVER_CMD || "";
const hostServerArgs = process.env.MATCHMAKER_SERVER_ARGS || "";

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
const rateLimiter = new Map();
const hostSupervisor = supervisorEnabled
  ? createHostSupervisor({
      hostAddress,
      hostBasePort,
      hostMaxRooms,
      hostRestartMax,
      hostRestartBackoffMs,
      hostServerCmd,
      hostServerArgs,
    })
  : null;

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

function isRateLimited(req, routeKey) {
  const ip = req.socket?.remoteAddress || "unknown";
  const key = `${ip}:${routeKey}`;
  const now = Date.now();
  let entry = rateLimiter.get(key);
  if (!entry || now - entry.windowStart > rateLimitWindowMs) {
    entry = { windowStart: now, count: 0 };
  }
  entry.count += 1;
  rateLimiter.set(key, entry);
  return entry.count > rateLimitMax;
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
  try {
    if (!token || typeof token !== "string") return null;
    const [encoded, signature] = token.split(".");
    if (!encoded || !signature) return null;

    const body = base64UrlDecode(encoded);
    if (!body) return null;

    const expected = crypto.createHmac("sha256", secret).update(body).digest("base64url");
    const providedBytes = Buffer.from(signature);
    const expectedBytes = Buffer.from(expected);
    if (providedBytes.length !== expectedBytes.length) return null;
    if (!crypto.timingSafeEqual(providedBytes, expectedBytes)) return null;

    const payload = JSON.parse(body);
    if (!payload || typeof payload !== "object") return null;
    if (payload.nbf && Date.now() < payload.nbf) return null;
    if (payload.exp && Date.now() > payload.exp) return null;
    return payload;
  } catch {
    return null;
  }
}

function getRemoteConfig() {
  const now = Date.now();
  if (configCache && now - configCacheAt < configCacheTtlMs) {
    return configCache;
  }

  try {
    const raw = fs.readFileSync(configPath, "utf8");
    const parsed = JSON.parse(raw);
    configCache = mergeConfigDefaults(parsed);
    configCacheAt = now;
    return configCache;
  } catch {
    configCache = mergeConfigDefaults(null);
    configCacheAt = now;
    return configCache;
  }
}

function mergeConfigDefaults(config) {
  const defaults = {
    minigame_pool: ["arena_v1", "race_v1", "coinrush_v1", "stub_v1"],
    blocked_minigames: [],
    fallback_minigame_id: "stub_v1",
    max_players: 8,
    match_duration_s: 600,
  };

  if (!config || typeof config !== "object") {
    return defaults;
  }

  return {
    minigame_pool: Array.isArray(config.minigame_pool) && config.minigame_pool.length > 0
      ? config.minigame_pool
      : defaults.minigame_pool,
    blocked_minigames: Array.isArray(config.blocked_minigames) ? config.blocked_minigames : defaults.blocked_minigames,
    fallback_minigame_id: config.fallback_minigame_id || defaults.fallback_minigame_id,
    max_players: Number.isFinite(config.max_players) && config.max_players > 0 ? config.max_players : defaults.max_players,
    match_duration_s: Number.isFinite(config.match_duration_s) && config.match_duration_s > 0
      ? config.match_duration_s
      : defaults.match_duration_s,
  };
}

function selectMinigameId(requestedId, config) {
  const blocked = new Set(config.blocked_minigames || []);
  let selected = requestedId || pickRandom(config.minigame_pool);

  if (blocked.has(selected)) {
    selected = pickFallbackMinigame(config, blocked);
    log("minigame_blocked", { requested_id: requestedId, fallback_id: selected });
  }

  if (shouldUseFallback(config)) {
    const fallback = config.fallback_minigame_id;
    if (fallback && fallback !== selected) {
      log("minigame_fallback", { original_id: selected, fallback_id: fallback, crash_rate: metrics.zombiesWindow.length });
      selected = fallback;
    }
  }

  return selected || "stub_v1";
}

function pickFallbackMinigame(config, blocked) {
  const pool = (config.minigame_pool || []).filter(id => !blocked.has(id));
  if (pool.length > 0) {
    return pickRandom(pool);
  }

  if (config.fallback_minigame_id && !blocked.has(config.fallback_minigame_id)) {
    return config.fallback_minigame_id;
  }

  return "stub_v1";
}

function shouldUseFallback(config) {
  if (!config || !config.fallback_minigame_id) {
    return false;
  }

  return metrics.zombiesWindow.length >= crashRateThreshold;
}

function pickRandom(pool) {
  if (!pool || pool.length === 0) {
    return null;
  }

  const index = Math.floor(Math.random() * pool.length);
  return pool[index];
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
  const allocation = allocateMatchHost(matchId);
  if (!allocation) {
    return null;
  }

  const config = getRemoteConfig();
  const selectedMinigame = selectMinigameId(minigame_id, config);
  const resolvedMaxPlayers = Number.isFinite(max_players) && max_players > 0
    ? max_players
    : config.max_players || 16;

  metrics.matchesCreated += 1;
  const match = {
    match_id: matchId,
    minigame_id: selectedMinigame,
    status: "waiting",
    created_at: new Date().toISOString(),
    max_players: resolvedMaxPlayers,
    players: 0,
    endpoint: allocation.endpoint,
    server_endpoint: allocation.endpoint,
    last_heartbeat: Date.now(),
  };
  matches.set(matchId, match);
  if (allocation.activeMatches != null) {
    allocation.activeMatches += 1;
  }
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
  let meta = null;
  try {
    const url = new URL(req.url, `http://${req.headers.host}`);
    const path = url.pathname;
    const method = req.method?.toUpperCase() || "GET";
    meta = { method, path };
    if (method === "POST" && isRateLimited(req, path)) {
      log("rate_limited", { path, ip: req.socket?.remoteAddress || "unknown" });
      return respond(res, requestStart, 429, { error: "rate_limited" }, meta);
    }

    if (method === "GET" && path === "/health") {
      const uptime = Math.floor((Date.now() - startedAt) / 1000);
      return respond(res, requestStart, 200, {
        status: "ok",
        uptime_s: uptime,
        matches: matches.size,
        build_version: buildVersion,
      }, meta);
    }

    if (method === "GET" && path === "/config") {
      const config = getRemoteConfig();
      return respond(res, requestStart, 200, config, meta);
    }

    if (method === "POST" && path === "/matches") {
      const body = await readBody(req);
      const payload = body ? JSON.parse(body) : {};
      if (countActiveMatches() >= maxMatches) {
        log("match_allocation_failed", { reason: "capacity", max_matches: maxMatches });
        return respond(res, requestStart, 503, { error: "allocation_failed" }, meta);
      }
      const match = createMatch(payload);
      if (!match) {
        log("match_allocation_failed", { reason: "server_pool_exhausted" });
        return respond(res, requestStart, 503, { error: "allocation_failed" }, meta);
      }
      const hostToken = signToken({
        match_id: match.match_id,
        player_id: "host",
        nbf: Date.now() - 1000,
        exp: Date.now() + tokenTtlSeconds * 1000,
      });
      log("match_created", { match_id: match.match_id, minigame_id: match.minigame_id });
      return respond(res, requestStart, 200, {
        match_id: match.match_id,
        endpoint: match.endpoint,
        join_token: hostToken,
        status: match.status,
      }, meta);
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
      return respond(res, requestStart, 200, list, meta);
    }

    const matchJoin = path.match(/^\/matches\/([^/]+)\/join$/);
    if (method === "POST" && matchJoin) {
      const matchId = matchJoin[1];
      const match = matches.get(matchId);
      if (!match) return respond(res, requestStart, 404, { error: "match_not_found" }, meta);
      if (match.status === "ended") return respond(res, requestStart, 410, { error: "match_ended" }, meta);
      if (match.players >= match.max_players) return respond(res, requestStart, 409, { error: "match_full" }, meta);
      const playerId = generatePlayerId();
      match.players += 1;
      incrementServerPlayers(match);
      const joinToken = signToken({
        match_id: match.match_id,
        player_id: playerId,
        nbf: Date.now() - 1000,
        exp: Date.now() + tokenTtlSeconds * 1000,
      });
      log("match_join", { match_id: match.match_id, player_id: playerId });
      return respond(res, requestStart, 200, { endpoint: match.endpoint, join_token: joinToken }, meta);
    }

    const matchEnd = path.match(/^\/matches\/([^/]+)\/end$/);
    if (method === "POST" && matchEnd) {
      const matchId = matchEnd[1];
      const match = matches.get(matchId);
      if (!match) return respond(res, requestStart, 404, { error: "match_not_found" }, meta);
      const body = await readBody(req);
      const payload = body ? JSON.parse(body) : {};
      match.status = "ended";
      match.ended_at = new Date().toISOString();
      match.end_reason = payload.reason || "unknown";
      releaseMatch(match);
      log("match_ended", { match_id: matchId, reason: match.end_reason });
      return respond(res, requestStart, 200, { status: "ok" }, meta);
    }

    const matchHeartbeat = path.match(/^\/matches\/([^/]+)\/heartbeat$/);
    if (method === "POST" && matchHeartbeat) {
      const matchId = matchHeartbeat[1];
      const match = matches.get(matchId);
      if (!match) return respond(res, requestStart, 404, { error: "match_not_found" }, meta);
      match.last_heartbeat = Date.now();
      if (match.status === "waiting") match.status = "active";
      log("match_heartbeat", { match_id: matchId });
      return respond(res, requestStart, 200, { status: "ok" }, meta);
    }

    if (method === "POST" && path === "/tokens/verify") {
      const body = await readBody(req);
      const payload = body ? JSON.parse(body) : {};
      const tokenPayload = verifyToken(payload.token);
      if (!tokenPayload) return respond(res, requestStart, 401, { error: "invalid_token" }, meta);
      return respond(res, requestStart, 200, tokenPayload, meta);
    }

    if (method === "GET" && path === "/metrics") {
      return respondText(res, requestStart, 200, buildMetrics(), meta);
    }

    if (method === "GET" && path === "/dashboard") {
      return respondText(res, requestStart, 200, buildDashboard(), meta);
    }

    return respond(res, requestStart, 404, { error: "not_found" }, meta);
  } catch (err) {
    logError("matchmaker_error", { message: err.message });
    return respond(res, requestStart, 500, { error: "server_error" }, meta);
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

function respond(res, start, statusCode, body, meta) {
  recordRequest(start, statusCode, meta);
  return json(res, statusCode, body);
}

function respondText(res, start, statusCode, body, meta) {
  recordRequest(start, statusCode, meta);
  res.writeHead(statusCode, {
    "Content-Type": "text/plain; charset=utf-8",
    "Content-Length": Buffer.byteLength(body),
  });
  res.end(body);
}

function recordRequest(start, statusCode, meta) {
  const durationMs = Number(process.hrtime.bigint() - start) / 1e6;
  metrics.requestsTotal += 1;
  metrics.requestDurationMsTotal += durationMs;
  metrics.requestDurationMsMax = Math.max(metrics.requestDurationMsMax, durationMs);
  if (meta && meta.method && meta.path) {
    log("http_request", {
      method: meta.method,
      path: meta.path,
      status_code: statusCode,
      duration_ms: Math.round(durationMs * 100) / 100,
    });
  }
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

function createHostSupervisor(options) {
  const supervisor = new HostSupervisor(options);
  supervisor.init();
  return supervisor;
}

class HostSupervisor {
  constructor(options) {
    this.hostAddress = options.hostAddress;
    this.hostBasePort = options.hostBasePort;
    this.hostMaxRooms = options.hostMaxRooms;
    this.hostRestartMax = options.hostRestartMax;
    this.hostRestartBackoffMs = options.hostRestartBackoffMs;
    this.hostServerCmd = options.hostServerCmd;
    this.hostServerArgs = options.hostServerArgs;
    this.rooms = new Map();
    this.availablePorts = [];
  }

  init() {
    for (let i = 0; i < this.hostMaxRooms; i += 1) {
      this.availablePorts.push(this.hostBasePort + i);
    }
  }

  allocate(matchId) {
    if (!this.hostServerCmd) {
      logError("host_supervisor_missing_cmd", { match_id: matchId });
      return null;
    }

    if (!this.availablePorts.length) {
      log("host_supervisor_capacity_exhausted", { match_id: matchId, capacity: this.hostMaxRooms });
      return null;
    }

    const port = this.availablePorts.shift();
    const room = {
      matchId,
      port,
      restarts: 0,
      stopping: false,
      process: null,
    };
    room.process = this.spawnRoom(room);
    this.rooms.set(matchId, room);
    return { endpoint: `${this.hostAddress}:${port}` };
  }

  release(matchId) {
    const room = this.rooms.get(matchId);
    if (!room) {
      return;
    }

    room.stopping = true;
    if (room.process && !room.process.killed) {
      room.process.kill();
    }
    this.rooms.delete(matchId);
    this.availablePorts.push(room.port);
  }

  spawnRoom(room) {
    const logDir = path.join(process.cwd(), "logs");
    fs.mkdirSync(logDir, { recursive: true });
    const stdoutPath = path.join(logDir, `host_${room.matchId}.log`);
    const stderrPath = path.join(logDir, `host_${room.matchId}_err.log`);
    const stdout = fs.createWriteStream(stdoutPath, { flags: "a" });
    const stderr = fs.createWriteStream(stderrPath, { flags: "a" });

    const args = buildServerArgs(this.hostServerArgs, room.matchId, room.port);
    const child = spawn(this.hostServerCmd, args, {
      stdio: ["ignore", stdout, stderr],
    });

    child.on("exit", (code, signal) => {
      if (room.stopping) {
        return;
      }

      if (room.restarts >= this.hostRestartMax) {
        logError("host_room_crashed", { match_id: room.matchId, code, signal });
        return;
      }

      room.restarts += 1;
      const backoff = this.hostRestartBackoffMs * room.restarts;
      log("host_room_restart", { match_id: room.matchId, attempt: room.restarts, backoff_ms: backoff });
      setTimeout(() => {
        if (room.stopping) {
          return;
        }
        room.process = this.spawnRoom(room);
      }, backoff).unref();
    });

    return child;
  }
}

function buildServerArgs(rawArgs, matchId, port) {
  if (!rawArgs) {
    return [];
  }

  const parts = rawArgs.match(/(?:[^\s"]+|"[^"]*")+/g) || [];
  return parts.map(token =>
    token
      .replaceAll("{match_id}", matchId)
      .replaceAll("{port}", `${port}`)
      .replace(/^"(.*)"$/, "$1")
  );
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

function allocateMatchHost(matchId) {
  if (hostSupervisor) {
    return hostSupervisor.allocate(matchId);
  }

  return allocateServer();
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
  if (hostSupervisor) return;
  const server = getServerByEndpoint(match.server_endpoint);
  if (!server) return;
  server.activePlayers += 1;
}

function releaseMatch(match) {
  if (!match || !match.server_endpoint) return;
  if (hostSupervisor) {
    hostSupervisor.release(match.match_id);
    return;
  }
  const server = getServerByEndpoint(match.server_endpoint);
  if (!server) return;
  server.activeMatches = Math.max(0, server.activeMatches - 1);
  if (match.players) {
    server.activePlayers = Math.max(0, server.activePlayers - match.players);
  }
}
