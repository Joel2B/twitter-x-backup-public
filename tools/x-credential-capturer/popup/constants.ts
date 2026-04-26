import type { EndpointDefinition, RequiredHeaderKey } from "./models.js";

export const ENDPOINTS: EndpointDefinition[] = [
  {
    id: "posts",
    title: "Api.posts (UserTweets)",
    type: "api",
    jsonKey: "posts",
    pageUrlTemplate: "https://x.com/{username}",
    enabledByDefault: true
  },
  {
    id: "likes",
    title: "Api.likes (Likes)",
    type: "api",
    jsonKey: "likes",
    pageUrlTemplate: "https://x.com/{username}/likes",
    enabledByDefault: true
  },
  {
    id: "bookmarks",
    title: "Api.bookmarks (Bookmarks)",
    type: "api",
    jsonKey: "bookmarks",
    pageUrl: "https://x.com/i/bookmarks",
    enabledByDefault: true
  },
  {
    id: "media",
    title: "Api.media (UserMedia)",
    type: "api",
    jsonKey: "UserMedia",
    pageUrlTemplate: "https://x.com/{username}/media",
    enabledByDefault: true
  },
  {
    id: "UserByScreenName",
    title: "Api.UserByScreenName",
    type: "api",
    jsonKey: "UserByScreenName",
    pageUrlTemplate: "https://x.com/{username}/media",
    enabledByDefault: true
  },
  {
    id: "TweetDetail",
    title: "Api.TweetDetail",
    type: "api",
    jsonKey: "TweetDetail",
    pageUrl: "https://x.com/AmeDollVT/status/2047352297571725658",
    enabledByDefault: true
  },
  {
    id: "UsersByRestIds",
    title: "Api.UsersByRestIds (unused)",
    type: "api",
    jsonKey: "UsersByRestIds",
    pageUrl: null,
    enabledByDefault: false,
    skipped: true
  }
];

export const REQUIRED_HEADERS: RequiredHeaderKey[] = [
  "authorization",
  "x-csrf-token",
  "cookie",
  "x-client-transaction-id",
  "Referer",
  "x-twitter-auth-type",
  "x-twitter-active-user",
  "x-twitter-client-language"
];

export const GRID_COLUMN_COUNT = 5;
export const CAPTURE_STATE_STORAGE_KEY = "captureStateV1";
export const SETTINGS_STORAGE_KEY = "captureSettingsV1";
export const PROFILES_STORAGE_KEY = "captureProfilesV1";
export const DEFAULT_PROFILE_ID = "default";
