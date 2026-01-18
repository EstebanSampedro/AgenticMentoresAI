import { getAuthenticatedToken } from "../context/ChatContext";
// ---------- Tipos ----------

export type Severity = 0 | 1 | 2 | 3 | 4 | 5;

export type LoggerInit = {
  logsBaseUrl: string;
  getUserId?: () => string | null;
  getChatId?: () => string | null;
  appInfo?: { appName?: string; appVersion?: string; buildId?: string; env?: string };
};
export type LogInput = {
  message: string;
  severity?: Severity;
  error?: unknown;
  extra?: Record<string, unknown>;
  timestamp?: string;
};

// ---------- Constantes / utils ----------
const STORAGE_KEY = "clientLogQueue:v1";
const MAX_QUEUE_ITEMS = 200;
const MAX_QUEUE_BYTES = 400_000; // ~400 KB
const MAX_CONTEXT_BYTES = 16_000;
const API_BASE = import.meta.env.VITE_API_BASE;

function nowISO() {
  return new Date().toISOString();
}
function safeStringify(obj: unknown) {
  try {
    return JSON.stringify(obj);
  } catch {
    return "[unstringifiable]";
  }
}
function truncateBytes(s: string, max: number) {
  if (s.length <= max) return s;
  return s.slice(0, max) + "…";
}
function normalizeError(err: unknown) {
  if (err instanceof Error) {
    return {
      errorName: err.name,
      errorMessage: err.message,
      stack: err.stack,
    };
  }
  if (typeof err === "object" && err !== null) {
    return { thrown: safeStringify(err) };
  }
  return { thrown: String(err) };
}

// ---------- Implementación ----------
export class ClientLogger {
  private getUserId?: LoggerInit["getUserId"];
  private getChatId?: LoggerInit["getChatId"];
  private appInfo?: LoggerInit["appInfo"];
  private baseUrl: string;

  constructor(cfg: LoggerInit) {
    this.getUserId = cfg.getUserId;
    this.getChatId = cfg.getChatId;
    this.appInfo = cfg.appInfo;
    this.baseUrl = (cfg.logsBaseUrl || "").replace(/\/+$/, "");

    window.addEventListener("online", () => this.flushQueue());
    document.addEventListener("visibilitychange", () => {
      if (document.visibilityState === "visible") this.flushQueue();
    });
    window.addEventListener("pagehide", () => this.flushQueue());

    this.flushQueue();
  }

  async log(input: LogInput) {
    const ctx = {
      ...this.appInfo,
      url: window.location.href,
      route: window.location.pathname,
      userAgent: navigator.userAgent,
      language: navigator.language,
      ...input.extra,
      ...(input.error ? normalizeError(input.error) : {}),
    };

    const payload = {
      Message: input.message,
      UserId: this.getUserId?.() ?? "unknown",
      ChatId: this.getChatId?.() ?? "",
      Severity: input.severity ?? 3,
      Context: truncateBytes(safeStringify(ctx), MAX_CONTEXT_BYTES),
      Timestamp: input.timestamp ?? nowISO(),
    };

    const ok = await this.trySend(payload);
    if (!ok) this.enqueue(payload);
  }

  info(message: string, extra?: Record<string, unknown>) {
    return this.log({ message, severity: 2, extra });
  }
  warn(message: string, extra?: Record<string, unknown>) {
    return this.log({ message, severity: 3, extra });
  }
  error(message: string, error?: unknown, extra?: Record<string, unknown>) {
    return this.log({ message, severity: 4, error, extra });
  }

  private endpoint() {
    return `${API_BASE}/api/client-logs`;
  }
  

  private async trySend(payload: any): Promise<boolean> {
  const controller = new AbortController();
  const id = window.setTimeout(() => controller.abort(), 5000);

  

  try {

    let token = await getAuthenticatedToken();
    const baseHeaders: Record<string, string> = {
      "Accept": "application/json",
      "Content-Type": "application/json",
      "Authorization": `Bearer ${token}`,
    };

    const doPost = (headers: Record<string, string>) =>
      fetch(this.endpoint(), {
        method: "POST",
        headers,
        body: JSON.stringify(payload),
        keepalive: true,
        signal: controller.signal,
        // credentials: "include", // habilítalo si además usas cookie
      });

    // 1er intento
    let res = await doPost(baseHeaders);   

    if (res.ok) return true;
    console.warn("Logger: HTTP no-OK", res.status, res.statusText);
  } catch (err) {
    console.log("Logger: envío fallido, se encola", err);
  } finally {
    clearTimeout(id);
  }

  // Fallback same-origin (sin headers). No funciona cross-origin.
  try {
    const blob = new Blob([JSON.stringify(payload)], { type: "application/json" });
    const sent = navigator.sendBeacon?.(this.endpoint(), blob);
    if (sent) return true;
  } catch (e) {
    console.warn("Logger: sendBeacon falló", e);
  }

  return false;
}


  private readQueue(): any[] {
    try {
      return JSON.parse(localStorage.getItem(STORAGE_KEY) || "[]");
    } catch {
      return [];
    }
  }
  private writeQueue(items: any[]) {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(items));
  }
  private enqueue(payload: any) {
    const q = this.readQueue();
    q.push(payload);

    if (q.length > MAX_QUEUE_ITEMS) {
      q.splice(0, q.length - MAX_QUEUE_ITEMS);
    }

    let serialized = JSON.stringify(q);
    while (serialized.length > MAX_QUEUE_BYTES && q.length > 1) {
      q.shift();
      serialized = JSON.stringify(q);
    }

    this.writeQueue(q);
  }

  async flushQueue() {
    const q = this.readQueue();
    if (!q.length) return;

    const remaining: any[] = [];
    for (const item of q) {
      const ok = await this.trySend(item);
      if (!ok) remaining.push(item);
    }
    this.writeQueue(remaining);
  }
}

// ---------- Singleton / API ----------
let _logger: ClientLogger | null = null;

/** Llamar una sola vez (antes de renderizar la app) */
export function initClientLogger(cfg: LoggerInit) {
  _logger = new ClientLogger(cfg);

  window.addEventListener("error", (ev) => {
    _logger?.error(ev.message || "window.onerror", ev.error, {
      component: "window",
      fingerprint: `${ev.filename}:${ev.lineno}:${ev.colno}`,
    });
  });

  window.addEventListener("unhandledrejection", (ev: PromiseRejectionEvent) => {
    _logger?.error("unhandledrejection", ev.reason, { component: "window" });
  });

  return _logger;
}

/** Obtiene el singleton ya inicializado */
export function logger(): ClientLogger {
  if (!_logger) {
    const noop = {
      log: async () => {},
      info: async () => {},
      warn: async () => {},
      error: async () => {},
      flushQueue: async () => {},
    } as unknown as ClientLogger;
    return noop;
  }
  return _logger;
}
