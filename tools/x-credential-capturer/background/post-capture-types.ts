import type {
  CapturedPostItem,
  CapturedPostsStore,
  ProcessedPost,
  ProcessedPostMedia,
  ProcessedPostProfile,
  ProcessedPostVariant,
  UploadNotificationItem,
  UploadNotificationsStore,
  UploadCapturedPostsSummary,
  UploadCapturedPostsMessageResponse
} from "../popup/models.js";

export type CaptureResponseBodyPayload = {
  url: string;
  status?: number;
  body: string;
  capturedAt?: string;
};

export type UploadApiDiagnosticsPayload = {
  beforeCount: number | null;
  afterCount: number | null;
  deltaCount: number | null;
  ignoredPosts: number | null;
  durationMs: number | null;
};

export type UploadApiResponsePayload = {
  receivedPosts: number | null;
  savedPosts: number | null;
  diagnostics: UploadApiDiagnosticsPayload | null;
};

export type {
  CapturedPostItem,
  CapturedPostsStore,
  ProcessedPost,
  ProcessedPostMedia,
  ProcessedPostProfile,
  ProcessedPostVariant,
  UploadNotificationItem,
  UploadNotificationsStore,
  UploadCapturedPostsSummary,
  UploadCapturedPostsMessageResponse
};
