export const STORAGE_KEY = "captureStateV1";
export const CAPTURED_POSTS_STORAGE_KEY = "capturedPostsV1";
export const DEFAULT_UPLOAD_API_BASE_URL = "http://127.0.0.1:5085";
export const DEFAULT_UPLOAD_ORIGIN = "extension-search-timeline";

export const OPERATION_TO_ENDPOINT = {
  UserTweets: "posts",
  Likes: "likes",
  Bookmarks: "bookmarks",
  UserMedia: "media",
  SearchTimeline: "SearchTimeline",
  UserByScreenName: "UserByScreenName",
  TweetDetail: "TweetDetail",
  UsersByRestIds: "UsersByRestIds"
} as const;

export type OperationName = keyof typeof OPERATION_TO_ENDPOINT;
export type EndpointId = (typeof OPERATION_TO_ENDPOINT)[OperationName];

export const ENDPOINT_IDS: EndpointId[] = Array.from(
  new Set(Object.values(OPERATION_TO_ENDPOINT))
) as EndpointId[];

export const GRAPHQL_FILTER = {
  urls: ["https://x.com/i/api/graphql/*"]
};

export const CAPTURE_HISTORY_LIMIT = 5;
