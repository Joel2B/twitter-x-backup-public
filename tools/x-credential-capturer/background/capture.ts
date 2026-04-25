import type {
  CaptureState,
  CapturedRate,
  EndpointCapture,
  EndpointHistoryEntry,
  JsonObject,
  RollbackMessageResponse
} from "../popup/models.js";
import { CAPTURE_HISTORY_LIMIT, ENDPOINT_IDS, OPERATION_TO_ENDPOINT } from "./constants.js";
import {
  buildCookieHeader,
  getCookieValue,
  isPlainObject,
  normalizeQueryState,
  nowIso,
  parseOperation,
  parseQueryId,
  parseRequestQuery,
  toHeaderMap
} from "./utils.js";
import {
  defaultEndpointState,
  emptyRateState,
  emptyResponseState,
  getState,
  saveState
} from "./state.js";

type RequestCaptureDetails = {
  url: string;
  requestHeaders?: chrome.webRequest.HttpHeader[];
};

type ResponseCaptureDetails = {
  url: string;
  statusCode?: number;
  responseHeaders?: chrome.webRequest.HttpHeader[];
};

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value));
}

function hasEndpointData(endpoint: EndpointCapture | null | undefined): boolean {
  if (!endpoint) {
    return false;
  }

  return Boolean(
    endpoint.captured ||
    endpoint.url ||
    endpoint.lastSeenAt ||
    endpoint.queryId ||
    endpoint.operation ||
    endpoint.response?.lastSeenAt ||
    endpoint.response?.status
  );
}

function cloneEndpointSnapshot(endpoint: EndpointCapture | null | undefined): JsonObject {
  const cloned = cloneJson(endpoint || defaultEndpointState()) as JsonObject;

  if (isPlainObject(cloned.headers)) {
    cloned.headers = {
      ...cloned.headers,
      all: {}
    };
  }

  if (isPlainObject(cloned.response)) {
    cloned.response = {
      ...cloned.response,
      headers: {}
    };
  }

  delete cloned.history;
  return cloned;
}

function withHistory(
  previousEndpoint: EndpointCapture | null | undefined,
  source: string
): EndpointHistoryEntry[] {
  const historyBase = Array.isArray(previousEndpoint?.history) ? [...previousEndpoint.history] : [];
  const trimmed = historyBase.slice(-(CAPTURE_HISTORY_LIMIT - 1));

  if (!hasEndpointData(previousEndpoint)) {
    return trimmed;
  }

  trimmed.push({
    at: nowIso(),
    source,
    snapshot: cloneEndpointSnapshot(previousEndpoint)
  });

  return trimmed.slice(-CAPTURE_HISTORY_LIMIT);
}

function recomputeGlobalHeaders(state: CaptureState): void {
  let cookie: string | null = null;
  let csrf: string | null = null;
  let authorization: string | null = null;

  for (const endpointId of ENDPOINT_IDS) {
    const endpoint = state.endpoints[endpointId];

    if (!endpoint) {
      continue;
    }

    if (!cookie && endpoint.headers.cookie) {
      cookie = endpoint.headers.cookie;
    }

    if (!csrf && endpoint.headers["x-csrf-token"]) {
      csrf = endpoint.headers["x-csrf-token"];
    }

    if (!authorization && endpoint.headers.authorization) {
      authorization = endpoint.headers.authorization;
    }
  }

  if (!csrf && cookie) {
    csrf = getCookieValue(cookie, "ct0");
  }

  state.global.cookie = cookie;
  state.global.xCsrfToken = csrf;
  state.global.authorization = authorization;
}

function mapRateFromResponseHeaders(responseHeadersMap: Record<string, string>): CapturedRate {
  return {
    limit: responseHeadersMap["x-rate-limit-limit"] || null,
    remaining: responseHeadersMap["x-rate-limit-remaining"] || null,
    reset: responseHeadersMap["x-rate-limit-reset"] || null,
    responseTime: responseHeadersMap["x-response-time"] || null
  };
}

function resolveEndpointId(operation: string | null): string | null {
  if (!operation) {
    return null;
  }

  return OPERATION_TO_ENDPOINT[operation as keyof typeof OPERATION_TO_ENDPOINT] || null;
}

export async function captureFromRequest(details: RequestCaptureDetails): Promise<void> {
  const operation = parseOperation(details.url);
  const endpointId = resolveEndpointId(operation);

  if (!endpointId) {
    return;
  }

  const requestHeaders = toHeaderMap(details.requestHeaders || []);
  const state = await getState();
  const previousEndpoint = state.endpoints[endpointId] || defaultEndpointState();
  const endpoint: EndpointCapture = {
    ...defaultEndpointState(),
    ...previousEndpoint,
    headers: {
      ...defaultEndpointState().headers,
      ...(previousEndpoint.headers || {})
    },
    history: withHistory(previousEndpoint, "request")
  };

  endpoint.captured = true;
  endpoint.lastSeenAt = nowIso();
  endpoint.url = details.url;
  endpoint.queryId = parseQueryId(details.url);
  endpoint.operation = operation;

  const parsedQuery = parseRequestQuery(details.url);

  if (parsedQuery) {
    endpoint.query = normalizeQueryState(parsedQuery);
  }

  if (requestHeaders["x-client-transaction-id"]) {
    endpoint.headers["x-client-transaction-id"] = requestHeaders["x-client-transaction-id"];
  }

  endpoint.headers.all = requestHeaders;

  if (requestHeaders.referer) {
    endpoint.headers.referer = requestHeaders.referer;
  }

  if (requestHeaders["x-csrf-token"]) {
    endpoint.headers["x-csrf-token"] = requestHeaders["x-csrf-token"];
  }

  if (requestHeaders.cookie) {
    endpoint.headers.cookie = requestHeaders.cookie;
  }

  if (requestHeaders.authorization) {
    endpoint.headers.authorization = requestHeaders.authorization;
  }

  if (requestHeaders["x-twitter-auth-type"]) {
    endpoint.headers["x-twitter-auth-type"] = requestHeaders["x-twitter-auth-type"];
  }

  if (requestHeaders["x-twitter-active-user"]) {
    endpoint.headers["x-twitter-active-user"] = requestHeaders["x-twitter-active-user"];
  }

  if (requestHeaders["x-twitter-client-language"]) {
    endpoint.headers["x-twitter-client-language"] = requestHeaders["x-twitter-client-language"];
  }

  if (!endpoint.headers.cookie) {
    endpoint.headers.cookie = await buildCookieHeader();
  }

  if (!endpoint.headers["x-csrf-token"]) {
    endpoint.headers["x-csrf-token"] = getCookieValue(endpoint.headers.cookie, "ct0");
  }

  state.endpoints[endpointId] = endpoint;

  if (endpoint.headers.cookie) {
    state.global.cookie = endpoint.headers.cookie;
  }

  if (endpoint.headers["x-csrf-token"]) {
    state.global.xCsrfToken = endpoint.headers["x-csrf-token"];
  }

  if (endpoint.headers.authorization) {
    state.global.authorization = endpoint.headers.authorization;
  }

  await saveState(state);
}

export async function captureFromResponse(details: ResponseCaptureDetails): Promise<void> {
  const operation = parseOperation(details.url);
  const endpointId = resolveEndpointId(operation);

  if (!endpointId) {
    return;
  }

  const responseHeadersMap = toHeaderMap(details.responseHeaders || []);
  const state = await getState();
  const previousEndpoint = state.endpoints[endpointId] || defaultEndpointState();
  const endpoint: EndpointCapture = {
    ...defaultEndpointState(),
    ...previousEndpoint,
    response: {
      ...emptyResponseState(),
      ...(previousEndpoint.response || {}),
      headers: {
        ...(isPlainObject(previousEndpoint.response?.headers)
          ? previousEndpoint.response.headers
          : {})
      },
      rate: {
        ...emptyRateState(),
        ...(previousEndpoint.response?.rate || {})
      }
    },
    headers: {
      ...defaultEndpointState().headers,
      ...(previousEndpoint.headers || {})
    },
    history: Array.isArray(previousEndpoint.history) ? previousEndpoint.history : []
  };

  endpoint.response.lastSeenAt = nowIso();
  endpoint.response.status =
    typeof details.statusCode === "number" ? details.statusCode : endpoint.response.status;
  endpoint.response.headers = responseHeadersMap;
  endpoint.response.rate = mapRateFromResponseHeaders(responseHeadersMap);

  if (!endpoint.captured) {
    endpoint.captured = true;
  }

  if (!endpoint.lastSeenAt) {
    endpoint.lastSeenAt = nowIso();
  }

  if (!endpoint.url) {
    endpoint.url = details.url;
  }

  if (!endpoint.queryId) {
    endpoint.queryId = parseQueryId(details.url);
  }

  if (!endpoint.operation) {
    endpoint.operation = operation;
  }

  const parsedQuery = parseRequestQuery(details.url);

  if (parsedQuery) {
    const hasExistingQueryData = Boolean(
      Object.keys(endpoint.query?.Variables || {}).length ||
      Object.keys(endpoint.query?.Features || {}).length ||
      Object.keys(endpoint.query?.FieldToggles || {}).length
    );

    if (!hasExistingQueryData) {
      endpoint.query = normalizeQueryState(parsedQuery);
    }
  }

  state.endpoints[endpointId] = endpoint;
  await saveState(state);
}

function normalizeRestoredEndpoint(
  snapshot: unknown,
  history: EndpointHistoryEntry[]
): EndpointCapture {
  const base = defaultEndpointState();
  const source = isPlainObject(snapshot) ? snapshot : {};

  const restored: EndpointCapture = {
    ...base,
    ...source,
    query: normalizeQueryState(source.query),
    response: {
      ...emptyResponseState(),
      ...(isPlainObject(source.response) ? source.response : {}),
      headers:
        isPlainObject(source.response) && isPlainObject(source.response.headers)
          ? (source.response.headers as Record<string, string>)
          : {},
      rate: {
        ...emptyRateState(),
        ...(isPlainObject(source.response) && isPlainObject(source.response.rate)
          ? source.response.rate
          : {})
      }
    },
    headers: {
      ...base.headers,
      ...(isPlainObject(source.headers) ? source.headers : {}),
      all:
        isPlainObject(source.headers) && isPlainObject(source.headers.all)
          ? (source.headers.all as Record<string, string>)
          : {}
    },
    history: Array.isArray(history) ? history : []
  };

  return restored;
}

export async function rollbackEndpoint(endpointId: string): Promise<RollbackMessageResponse> {
  if (!ENDPOINT_IDS.includes(endpointId as (typeof ENDPOINT_IDS)[number])) {
    return { ok: false, error: "Invalid endpoint id" };
  }

  const state = await getState();
  const endpoint = state?.endpoints?.[endpointId] || defaultEndpointState();
  const history = Array.isArray(endpoint.history) ? endpoint.history : [];

  if (history.length === 0) {
    return { ok: false, error: "No history available for this endpoint" };
  }

  const rollbackEntry = history[history.length - 1];
  const remainingHistory = history.slice(0, -1);
  const restored = normalizeRestoredEndpoint(rollbackEntry?.snapshot || {}, remainingHistory);

  state.endpoints[endpointId] = restored;
  recomputeGlobalHeaders(state);
  await saveState(state);

  return { ok: true, state, restoredFrom: rollbackEntry?.at || null };
}
