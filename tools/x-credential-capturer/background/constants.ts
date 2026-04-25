export const STORAGE_KEY = "captureStateV1";

export const OPERATION_TO_ENDPOINT = {
  UserTweets: "posts",
  Likes: "likes",
  Bookmarks: "bookmarks",
  UserMedia: "UserMedia",
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
