export type JsonObject = Record<string, unknown>;

export type QueryData = Record<string, unknown>;

export type CapturedQuery = {
  Variables: QueryData;
  Features: QueryData;
  FieldToggles: QueryData;
};

export type CapturedRate = {
  limit: string | null;
  remaining: string | null;
  reset: string | null;
  responseTime: string | null;
};

export type CapturedResponse = {
  lastSeenAt: string | null;
  status: number | null;
  headers: Record<string, string>;
  rate: CapturedRate;
};

export type CapturedHeaders = {
  all: Record<string, string>;
  "x-client-transaction-id": string | null;
  referer: string | null;
  "x-csrf-token": string | null;
  cookie: string | null;
  authorization: string | null;
  "x-twitter-auth-type": string | null;
  "x-twitter-active-user": string | null;
  "x-twitter-client-language": string | null;
};

export type EndpointHistoryEntry = {
  at: string;
  source: string;
  snapshot: JsonObject;
};

export type EndpointCapture = {
  captured: boolean;
  lastSeenAt: string | null;
  url: string | null;
  queryId: string | null;
  operation: string | null;
  query: CapturedQuery;
  response: CapturedResponse;
  headers: CapturedHeaders;
  history: EndpointHistoryEntry[];
};

export type CaptureState = {
  updatedAt: string;
  global: {
    authorization: string | null;
    xCsrfToken: string | null;
    cookie: string | null;
  };
  endpoints: Record<string, EndpointCapture>;
};

export type GlobalHeaders = {
  authorization: string | null;
  "x-csrf-token": string | null;
  cookie: string | null;
};

export type RequestHeaders = Record<string, string | null>;

export type EndpointRequest = {
  Url: string | null;
  Query: CapturedQuery;
  Headers: RequestHeaders;
};

export type EndpointDefinition = {
  id: string;
  title: string;
  type: "api";
  jsonId?: string;
  jsonKey: string;
  pageUrlTemplate?: string;
  pageUrl?: string | null;
  enabledByDefault: boolean;
  skipped?: boolean;
};

export type RequiredHeaderKey =
  | "authorization"
  | "x-csrf-token"
  | "cookie"
  | "x-client-transaction-id"
  | "Referer"
  | "x-twitter-auth-type"
  | "x-twitter-active-user"
  | "x-twitter-client-language";

export type EndpointModel = {
  endpoint: EndpointDefinition;
  capture: EndpointCapture | null;
  request: EndpointRequest;
  missingFields: string[];
  captured: boolean;
  ready: boolean;
  historyCount: number;
};

export type ApiPatchEntry = {
  Id: string;
  Enabled: boolean;
  Request: EndpointRequest;
};

export type ApiPatch = {
  generatedAt: string;
  mode: "independent-apis";
  note: string;
  Api: Record<string, ApiPatchEntry>;
};

export type SingleApiPatch = {
  generatedAt: string;
  mode: "single-api";
  endpoint: string;
  type: string;
  Api: Record<string, ApiPatchEntry>;
};

export type EndpointTestResult = {
  ok: boolean;
  status: number;
  hasData: boolean;
  rate: CapturedRate | null;
  message: string;
  bodySnippet: string;
};

export type EndpointTestRuntime = {
  running: boolean;
  result: EndpointTestResult | null;
};

export type PopupSettings = {
  username: string;
  maskSensitive: boolean;
};

export type ProfileEntry = {
  id: string;
  name: string;
  state: CaptureState;
  settings: PopupSettings;
  updatedAt: string | null;
};

export type ProfilesStore = {
  activeProfileId: string;
  profiles: Record<string, ProfileEntry>;
};

export type FreshnessInfo = {
  className: string;
  label: string;
};

export type RuntimeMessageResponse<T = Record<string, never>> = {
  ok: boolean;
  error?: string;
} & T;

export type BackgroundMessage =
  | { type: "getState" }
  | { type: "clearState" }
  | { type: "setState"; state?: CaptureState }
  | { type: "refreshCookies" }
  | { type: "rollbackEndpoint"; endpointId?: string };

export type StateMessageResponse = { ok: true; state: CaptureState } | { ok: false; error: string };

export type RollbackMessageResponse =
  | { ok: true; state: CaptureState; restoredFrom: string | null }
  | { ok: false; error: string };
