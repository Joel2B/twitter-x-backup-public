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
  hashtag: string;
  maskSensitive: boolean;
  capturedPostsView: "list" | "grid";
  capturedPostsGridColumns: number;
  capturedPostsShowThumbnail: boolean;
  capturedPostsSort: "latest-added" | "oldest-added" | "last-seen";
};

export type ProfileEntry = {
  id: string;
  name: string;
  state: CaptureState;
  settings: PopupSettings;
  updatedAt: string | null;
};

export type ProcessedPostVariant = {
  contentType: string;
  bitrate?: number | null;
  url: string;
};

export type ProcessedPostVideoInfo = {
  durationMilis?: number | null;
  variants?: ProcessedPostVariant[] | null;
};

export type ProcessedPostMedia = {
  id: string;
  url: string;
  type: string;
  videoInfo?: ProcessedPostVideoInfo | null;
};

export type ProcessedPostProfile = {
  id: string;
  userName?: string | null;
  name?: string | null;
  bannerUrl?: string | null;
  imageUrl?: string | null;
  following?: boolean | null;
};

export type ProcessedPost = {
  id: string;
  profile: ProcessedPostProfile;
  description: string;
  retweeted: boolean;
  favorited: boolean;
  bookmarked: boolean;
  createdAt: string;
  hashtags?: string[] | null;
  medias?: ProcessedPostMedia[] | null;
  deleted?: boolean;
};

export type CapturedPostItem = {
  id: string;
  operation: string;
  capturedAt: string;
  lastSeenAt: string;
  uploadedAt: string | null;
  text: string | null;
  mediaUrls: string[];
  authorUserName: string | null;
  authorName: string | null;
  authorId: string;
  postUrl: string | null;
  processed: ProcessedPost;
};

export type CapturedPostsStore = {
  updatedAt: string;
  apiBaseUrl: string;
  uploadUserId: string;
  uploadOrigin: string;
  captureHashtags: string[];
  items: Record<string, CapturedPostItem>;
};

export type UploadNotificationStatus = "running" | "completed" | "failed" | "expired";

export type UploadNotificationItem = {
  id: string;
  status: UploadNotificationStatus;
  createdAt: string;
  startedAt: string;
  completedAt: string | null;
  attemptedPosts: number;
  uploadedPosts: number;
  failedPosts: number;
  apiBaseUrl: string;
  uploadUserId: string;
  uploadOrigin: string;
  uploadSummary: UploadCapturedPostsSummary | null;
  error: string | null;
};

export type UploadNotificationsStore = {
  updatedAt: string;
  items: UploadNotificationItem[];
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
  | { type: "rollbackEndpoint"; endpointId?: string }
  | { type: "getCapturedPosts" }
  | {
      type: "setUploadTarget";
      apiBaseUrl?: string;
      uploadUserId?: string;
      uploadOrigin?: string;
      captureHashtags?: string[];
    }
  | { type: "uploadCapturedPosts"; ids?: string[] }
  | { type: "clearUploadedCapturedPosts" }
  | { type: "resetCapturedPostsUploadStatus" }
  | { type: "getUploadNotifications" }
  | { type: "clearUploadNotifications" }
  | { type: "importCapturedPosts"; payload?: unknown }
  | {
      type: "captureGraphqlResponseBody";
      url?: string;
      status?: number;
      body?: string;
      capturedAt?: string;
    };

export type StateMessageResponse = { ok: true; state: CaptureState } | { ok: false; error: string };

export type RollbackMessageResponse =
  | { ok: true; state: CaptureState; restoredFrom: string | null }
  | { ok: false; error: string };

export type CapturedPostsMessageResponse =
  | { ok: true; store: CapturedPostsStore }
  | { ok: false; error: string };

export type UploadNotificationsMessageResponse =
  | { ok: true; store: UploadNotificationsStore }
  | { ok: false; error: string };

export type UploadCapturedPostsSummary = {
  attemptedPosts: number;
  receivedPosts: number | null;
  savedPosts: number | null;
  ignoredPosts: number | null;
  beforeCount: number | null;
  afterCount: number | null;
  deltaCount: number | null;
  durationMs: number | null;
};

export type UploadCapturedPostsMessageResponse =
  | {
      ok: true;
      store: CapturedPostsStore;
      uploaded: string[];
      failed: string[];
      uploadSummary?: UploadCapturedPostsSummary | null;
    }
  | { ok: false; error: string };
