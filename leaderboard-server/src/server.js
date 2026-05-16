import http from "node:http";

const hostname = process.env.HOST ?? "127.0.0.1";
const port = Number.parseInt(process.env.PORT ?? "3000", 10);
const maxEntries = 5;

let leaderboard = [
  { name: "CRIS", score: 99450 },
  { name: "CLAUDE", score: 82100 },
  { name: "JAMMER42", score: 55000 },
  { name: "PLAYER1", score: 32400 },
];

const server = http.createServer(async (request, response) => {
  const url = new URL(request.url ?? "/", `http://${request.headers.host ?? "localhost"}`);

  if (request.method === "OPTIONS") {
    sendEmpty(response, 204);
    return;
  }

  if (request.method === "GET" && url.pathname === "/health") {
    sendJson(response, 200, { ok: true });
    return;
  }

  if (request.method === "GET" && url.pathname === "/leaderboard") {
    sendJson(response, 200, buildLeaderboardResponse());
    return;
  }

  if (request.method === "POST" && url.pathname === "/leaderboard") {
    try {
      const payload = await readJsonBody(request);
      const entry = normalizeEntry(payload);

      if (!entry) {
        sendJson(response, 400, { error: "Body must include a non-empty name and a numeric score." });
        return;
      }

      leaderboard = [...leaderboard, entry]
        .sort((left, right) => right.score - left.score)
        .slice(0, maxEntries);

      sendJson(response, 201, buildLeaderboardResponse());
    } catch {
      sendJson(response, 400, { error: "Invalid JSON body." });
    }

    return;
  }

  sendJson(response, 404, { error: "Not found" });
});

server.listen(port, hostname, () => {
  console.log(`Magnetic leaderboard server listening on http://${hostname}:${port}`);
});

function buildLeaderboardResponse() {
  return {
    entries: leaderboard.map((entry, index) => ({
      rank: index + 1,
      name: entry.name,
      score: entry.score
    }))
  };
}

function normalizeEntry(payload) {
  const name = String(payload?.name ?? "")
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9_#]/g, "")
    .slice(0, 16);
  const score = Number(payload?.score);

  if (!name || !Number.isFinite(score) || score < 0) {
    return null;
  }

  return {
    name,
    score: Math.round(score)
  };
}

async function readJsonBody(request) {
  let body = "";

  for await (const chunk of request) {
    body += chunk;

    if (body.length > 4096) {
      throw new Error("Request body too large.");
    }
  }

  return JSON.parse(body || "{}");
}

function sendJson(response, statusCode, payload) {
  const body = JSON.stringify(payload);

  response.writeHead(statusCode, {
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
    "Access-Control-Allow-Origin": "*",
    "Content-Length": Buffer.byteLength(body),
    "Content-Type": "application/json; charset=utf-8"
  });
  response.end(body);
}

function sendEmpty(response, statusCode) {
  response.writeHead(statusCode, {
    "Access-Control-Allow-Headers": "Content-Type",
    "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
    "Access-Control-Allow-Origin": "*"
  });
  response.end();
}
