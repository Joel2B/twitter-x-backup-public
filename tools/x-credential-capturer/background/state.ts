import type {
  CaptureState,
  CapturedQuery,
  CapturedRate,
  CapturedResponse,
  EndpointCapture,
  EndpointHistoryEntry,
  JsonObject
} from "../popup/models.js";
import { CAPTURE_HISTORY_LIMIT, ENDPOINT_IDS, STORAGE_KEY } from "./constants.js";
import { isPlainObject, normalizeQueryState, nowIso, toStringMapFromObject } from "./utils.js";

function asStringOrNull(value: unknown): string | null {
  return typeof value === "string" ? value : null;
}

function asNumberOrNull(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function asBoolean(value: unknown): boolean {
  return value === true;
}

export function emptyQueryState(): CapturedQuery {
  return {
    Variables: {},
    Features: {},
    FieldToggles: {}
  };
}

export function emptyRateState(): CapturedRate {
  return {
    limit: null,
    remaining: null,
    reset: null,
    responseTime: null
  };
}

export function emptyResponseState(): CapturedResponse {
  return {
    lastSeenAt: null,
    status: null,
    headers: {},
    rate: emptyRateState()
  };
}

export function defaultEndpointState(): EndpointCapture {
  return {
    captured: false,
    lastSeenAt: null,
    url: null,
    queryId: null,
    operation: null,
    query: emptyQueryState(),
    response: emptyResponseState(),
    headers: {
      all: {},
      "x-client-transaction-id": null,
      referer: null,
      "x-csrf-token": null,
      cookie: null,
      authorization: null,
      "x-twitter-auth-type": null,
      "x-twitter-active-user": null,
      "x-twitter-client-language": null
    },
    history: []
  };
}

export function defaultState(): CaptureState {
  const endpoints: Record<string, EndpointCapture> = {};

  for (const endpointId of ENDPOINT_IDS) {
    endpoints[endpointId] = defaultEndpointState();
  }

  return {
    updatedAt: nowIso(),
    global: {
      authorization: null,
      xCsrfToken: null,
      cookie: null
    },
    endpoints
  };
}

function sanitizeHistorySnapshot(snapshot: unknown): JsonObject {
  if (!isPlainObject(snapshot)) {
    return {};
  }

  const cloned: JsonObject = {
    ...snapshot,
    headers: isPlainObject(snapshot.headers)
      ? {
          ...snapshot.headers,
          all: {}
        }
      : {},
    response: isPlainObject(snapshot.response)
      ? {
          ...snapshot.response,
          headers: {}
        }
      : {}
  };

  delete cloned.history;
  return cloned;
}

function normalizeHistory(entries: unknown): EndpointHistoryEntry[] {
  const list = Array.isArray(entries) ? entries : [];
  const trimmed = list.slice(-CAPTURE_HISTORY_LIMIT);

  return trimmed.map((entry) => {
    const row = isPlainObject(entry) ? entry : {};

    return {
      at: typeof row.at === "string" ? row.at : nowIso(),
      source: typeof row.source === "string" ? row.source : "unknown",
      snapshot: sanitizeHistorySnapshot(row.snapshot)
    };
  });
}

function normalizeEndpoint(rawEndpoint: unknown): EndpointCapture {
  const base = defaultEndpointState();
  const source = isPlainObject(rawEndpoint) ? rawEndpoint : {};
  const responseSource = isPlainObject(source.response) ? source.response : {};
  const rateSource = isPlainObject(responseSource.rate) ? responseSource.rate : {};
  const headersSource = isPlainObject(source.headers) ? source.headers : {};

  return {
    ...base,
    captured: asBoolean(source.captured),
    lastSeenAt: asStringOrNull(source.lastSeenAt),
    url: asStringOrNull(source.url),
    queryId: asStringOrNull(source.queryId),
    operation: asStringOrNull(source.operation),
    query: normalizeQueryState(source.query),
    response: {
      ...base.response,
      lastSeenAt: asStringOrNull(responseSource.lastSeenAt),
      status: asNumberOrNull(responseSource.status),
      headers: toStringMapFromObject(responseSource.headers),
      rate: {
        ...base.response.rate,
        limit: asStringOrNull(rateSource.limit),
        remaining: asStringOrNull(rateSource.remaining),
        reset: asStringOrNull(rateSource.reset),
        responseTime: asStringOrNull(rateSource.responseTime)
      }
    },
    headers: {
      ...base.headers,
      all: toStringMapFromObject(headersSource.all),
      "x-client-transaction-id": asStringOrNull(headersSource["x-client-transaction-id"]),
      referer: asStringOrNull(headersSource.referer),
      "x-csrf-token": asStringOrNull(headersSource["x-csrf-token"]),
      cookie: asStringOrNull(headersSource.cookie),
      authorization: asStringOrNull(headersSource.authorization),
      "x-twitter-auth-type": asStringOrNull(headersSource["x-twitter-auth-type"]),
      "x-twitter-active-user": asStringOrNull(headersSource["x-twitter-active-user"]),
      "x-twitter-client-language": asStringOrNull(headersSource["x-twitter-client-language"])
    },
    history: normalizeHistory(source.history)
  };
}

export async function getState(): Promise<CaptureState> {
  const data = await chrome.storage.session.get(STORAGE_KEY);
  const storedRaw: unknown = data[STORAGE_KEY];

  if (!isPlainObject(storedRaw)) {
    return defaultState();
  }

  const base = defaultState();
  const globalSource = isPlainObject(storedRaw.global) ? storedRaw.global : {};
  const endpointsSource = isPlainObject(storedRaw.endpoints) ? storedRaw.endpoints : {};
  const state: CaptureState = {
    ...base,
    updatedAt: typeof storedRaw.updatedAt === "string" ? storedRaw.updatedAt : base.updatedAt,
    global: {
      authorization: asStringOrNull(globalSource.authorization),
      xCsrfToken: asStringOrNull(globalSource.xCsrfToken),
      cookie: asStringOrNull(globalSource.cookie)
    },
    endpoints: { ...base.endpoints }
  };

  for (const endpointId of ENDPOINT_IDS) {
    state.endpoints[endpointId] = normalizeEndpoint(endpointsSource[endpointId]);
  }

  return state;
}

export async function saveState(state: CaptureState): Promise<void> {
  const nextState: CaptureState = {
    ...state,
    updatedAt: nowIso()
  };
  await chrome.storage.session.set({ [STORAGE_KEY]: nextState });
}

export async function ensureStateInitialized(): Promise<void> {
  const existing = await chrome.storage.session.get(STORAGE_KEY);

  if (!existing[STORAGE_KEY]) {
    await saveState(defaultState());
  }
}
