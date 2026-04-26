import { ENDPOINTS, REQUIRED_HEADERS } from "./constants.js";
import type {
  ApiPatch,
  CaptureState,
  EndpointDefinition,
  EndpointModel,
  EndpointRequest,
  GlobalHeaders,
  RequestHeaders,
  SingleApiPatch
} from "./models.js";
import {
  cloneJson,
  cloneObject,
  hasAnyQueryData,
  normalizeCapturedQuery,
  normalizeUrlWithoutParams,
  parseCookieValue,
  pickFirstNonEmpty
} from "./utils.js";

export function resolveGlobalHeaders(state: CaptureState | null): GlobalHeaders {
  const result: GlobalHeaders = {
    authorization: state?.global?.authorization || null,
    "x-csrf-token": state?.global?.xCsrfToken || null,
    cookie: state?.global?.cookie || null
  };

  for (const endpoint of ENDPOINTS) {
    const capture = state?.endpoints?.[endpoint.id];

    if (!result.cookie && capture?.headers?.cookie) {
      result.cookie = capture.headers.cookie;
    }

    if (!result.authorization && capture?.headers?.authorization) {
      result.authorization = capture.headers.authorization;
    }

    if (!result["x-csrf-token"] && capture?.headers?.["x-csrf-token"]) {
      result["x-csrf-token"] = capture.headers["x-csrf-token"];
    }
  }

  if (!result["x-csrf-token"] && result.cookie) {
    result["x-csrf-token"] = parseCookieValue(result.cookie, "ct0");
  }

  return result;
}

export function buildIndependentHeaders(
  capture: CaptureState["endpoints"][string] | null | undefined,
  globalHeaders: GlobalHeaders
): RequestHeaders {
  const selected = capture?.headers;
  const rawHeaders = cloneObject(selected?.all) as RequestHeaders;
  const headers: RequestHeaders = { ...rawHeaders };

  const authorization = pickFirstNonEmpty(
    selected?.authorization,
    rawHeaders.authorization,
    globalHeaders.authorization
  );
  const csrf = pickFirstNonEmpty(
    selected?.["x-csrf-token"],
    rawHeaders["x-csrf-token"],
    globalHeaders["x-csrf-token"]
  );
  const cookie = pickFirstNonEmpty(selected?.cookie, rawHeaders.cookie, globalHeaders.cookie);
  const transaction = pickFirstNonEmpty(
    selected?.["x-client-transaction-id"],
    rawHeaders["x-client-transaction-id"]
  );
  const referer = pickFirstNonEmpty(selected?.referer, rawHeaders.referer, rawHeaders.Referer);
  const authType = pickFirstNonEmpty(
    selected?.["x-twitter-auth-type"],
    rawHeaders["x-twitter-auth-type"]
  );
  const activeUser = pickFirstNonEmpty(
    selected?.["x-twitter-active-user"],
    rawHeaders["x-twitter-active-user"]
  );
  const clientLanguage = pickFirstNonEmpty(
    selected?.["x-twitter-client-language"],
    rawHeaders["x-twitter-client-language"]
  );

  headers.authorization = authorization;
  headers["x-csrf-token"] = csrf;
  headers.cookie = cookie;
  headers["x-client-transaction-id"] = transaction;
  headers["x-twitter-auth-type"] = authType;
  headers["x-twitter-active-user"] = activeUser;
  headers["x-twitter-client-language"] = clientLanguage;

  delete headers.referer;
  headers.Referer = referer;

  return headers;
}

function computeMissingFields(endpoint: EndpointDefinition, request: EndpointRequest): string[] {
  if (endpoint.skipped) {
    return [];
  }

  const missing: string[] = [];

  if (!pickFirstNonEmpty(request?.Url)) {
    missing.push("Request.Url");
  }

  if (!hasAnyQueryData(request?.Query)) {
    missing.push("Request.Query(variables/features/fieldToggles)");
  }

  for (const headerKey of REQUIRED_HEADERS) {
    const value = request?.Headers?.[headerKey];

    if (!pickFirstNonEmpty(value)) {
      missing.push(`Request.Headers.${headerKey}`);
    }
  }

  return missing;
}

export function buildEndpointModel(
  endpoint: EndpointDefinition,
  state: CaptureState | null,
  globalHeaders: GlobalHeaders
): EndpointModel {
  const capture = state?.endpoints?.[endpoint.id] || null;
  const query = normalizeCapturedQuery(capture?.query);
  const request: EndpointRequest = {
    Url: normalizeUrlWithoutParams(capture?.url || null),
    Query: query,
    Headers: buildIndependentHeaders(capture, globalHeaders)
  };

  const missingFields = computeMissingFields(endpoint, request);
  const captured = Boolean(capture?.url);
  const ready = captured && missingFields.length === 0;

  return {
    endpoint,
    capture,
    request,
    missingFields,
    captured,
    ready,
    historyCount: Array.isArray(capture?.history) ? capture.history.length : 0
  };
}

export function createPatch(state: CaptureState | null): ApiPatch {
  const globalHeaders = resolveGlobalHeaders(state);
  const patch: ApiPatch = {
    generatedAt: new Date().toISOString(),
    mode: "independent-apis",
    note: "Each API includes a full Request with repeated headers and no dependency between endpoints.",
    Api: {}
  };

  for (const endpoint of ENDPOINTS) {
    const model = buildEndpointModel(endpoint, state, globalHeaders);

    if (!model.ready) {
      continue;
    }

    patch.Api[endpoint.jsonKey] = {
      Id: endpoint.id || model.endpoint.jsonKey,
      Enabled: endpoint.enabledByDefault,
      Request: model.request
    };
  }

  return patch;
}

export function createSingleEndpointPatch(model: EndpointModel): SingleApiPatch {
  const patch: SingleApiPatch = {
    generatedAt: new Date().toISOString(),
    mode: "single-api",
    endpoint: model.endpoint.jsonKey,
    type: model.endpoint.type,
    Api: {}
  };

  patch.Api[model.endpoint.jsonKey] = {
    Id: model.endpoint.id || model.endpoint.jsonKey,
    Enabled: model.endpoint.enabledByDefault,
    Request: model.request
  };

  return patch;
}

export function maskPatchSensitiveData(patch: ApiPatch): ApiPatch {
  const cloned = cloneJson(patch);
  const sensitiveHeaders = ["authorization", "cookie", "x-csrf-token", "x-client-transaction-id"];

  for (const entry of Object.values(cloned.Api)) {
    const headers = entry?.Request?.Headers;

    if (!headers || typeof headers !== "object") {
      continue;
    }

    for (const key of sensitiveHeaders) {
      if (typeof headers[key] === "string" && headers[key].trim()) {
        headers[key] = "***MASKED***";
      }
    }
  }

  return cloned;
}
