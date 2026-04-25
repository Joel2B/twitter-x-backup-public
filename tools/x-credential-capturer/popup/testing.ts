import type {
  CapturedRate,
  EndpointCapture,
  EndpointModel,
  EndpointRequest,
  EndpointTestResult,
  RequestHeaders
} from "./models.js";
import { getHeaderValueCaseInsensitive, pickFirstNonEmpty } from "./utils.js";

function buildRequestUrlForTest(request: EndpointRequest): string | null {
  const baseUrl = pickFirstNonEmpty(request?.Url);

  if (!baseUrl) {
    return null;
  }

  const parsed = new URL(baseUrl);
  parsed.searchParams.set("variables", JSON.stringify(request?.Query?.Variables || {}));
  parsed.searchParams.set("features", JSON.stringify(request?.Query?.Features || {}));
  parsed.searchParams.set("fieldToggles", JSON.stringify(request?.Query?.FieldToggles || {}));
  return parsed.toString();
}

function buildHeadersForTest(requestHeaders: RequestHeaders): Record<string, string> {
  const allowed = [
    "authorization",
    "x-csrf-token",
    "x-client-transaction-id",
    "x-twitter-auth-type",
    "x-twitter-active-user",
    "x-twitter-client-language",
    "accept"
  ];

  const headers: Record<string, string> = {};
  for (const name of allowed) {
    const value = getHeaderValueCaseInsensitive(requestHeaders, name);

    if (value) {
      headers[name] = value;
    }
  }

  if (!headers.accept) {
    headers.accept = "*/*";
  }

  return headers;
}

export async function runEndpointTest(model: EndpointModel): Promise<EndpointTestResult> {
  const url = buildRequestUrlForTest(model?.request);

  if (!url) {
    throw new Error("No Request.Url available for testing");
  }

  const headers = buildHeadersForTest(model.request?.Headers || {});
  const referrer = getHeaderValueCaseInsensitive(model.request?.Headers || {}, "Referer");
  const options: RequestInit & { referrer?: string } = {
    method: "GET",
    credentials: "include" as RequestCredentials,
    cache: "no-store" as RequestCache,
    headers
  };

  if (referrer) {
    options.referrer = referrer;
  }

  const response = await fetch(url, options);
  const content = await response.text();
  const hasData = /"data"/i.test(content);
  const ok = response.ok && hasData;
  const rate = {
    limit: response.headers.get("x-rate-limit-limit"),
    remaining: response.headers.get("x-rate-limit-remaining"),
    reset: response.headers.get("x-rate-limit-reset"),
    responseTime: response.headers.get("x-response-time")
  };
  const hasRateData = Object.values(rate).some((value) => pickFirstNonEmpty(value));

  return {
    ok,
    status: response.status,
    hasData,
    rate: hasRateData ? rate : null,
    message: ok
      ? `OK ${response.status}`
      : `Failed ${response.status}${hasData ? "" : " (no data)"}`,
    bodySnippet: content.slice(0, 180)
  };
}

function mapRateEntries(rate: CapturedRate | null | undefined): Array<[string, string | null]> {
  if (!rate) {
    return [];
  }

  const entries: Array<[string, string | null]> = [
    ["x-rate-limit-limit", rate.limit],
    ["x-rate-limit-remaining", rate.remaining],
    ["x-rate-limit-reset", rate.reset],
    ["x-response-time", rate.responseTime]
  ];

  return entries.filter((entry) => pickFirstNonEmpty(entry[1]));
}

export function getRateEntries(
  testResult: EndpointTestResult | null | undefined
): Array<[string, string | null]> {
  return mapRateEntries(testResult?.rate);
}

export function getCapturedRateEntries(
  capture: EndpointCapture | null | undefined
): Array<[string, string | null]> {
  return mapRateEntries(capture?.response?.rate);
}
