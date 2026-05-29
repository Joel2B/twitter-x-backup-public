import type {
  CaptureState,
  CapturedPostItem,
  CapturedPostsStore,
  EndpointDefinition,
  EndpointModel,
  EndpointTestRuntime,
  FreshnessInfo,
  PopupSettings,
  ProfileEntry
} from "../popup/models.js";

export type { PopupSettings };

export type ProfileRecord = ProfileEntry;

export type ProfilesStore = {
  activeProfileId: string;
  profiles: Record<string, ProfileRecord>;
};

export type ApplySettingsOptions = {
  syncInput?: boolean;
  scheduleSync?: boolean;
};

export type ApplyStateOptions = {
  resetTests?: boolean;
};

export type OpenUrlOptions = {
  active?: boolean;
  bypassCache?: boolean;
};

export type StatusBadge = {
  className: string;
  label: string;
};

export type EndpointRowView = {
  endpoint: EndpointDefinition;
  model: EndpointModel;
  endpointPageUrl: string | null;
  testRuntime: EndpointTestRuntime;
  testResultText: string;
  statusBadge: StatusBadge;
  freshness: FreshnessInfo;
  rateEntries: Array<[string, string | null]>;
  copyLabel: string;
  copyDisabled: boolean;
  copyTitle: string;
  testDisabled: boolean;
};

export type CapturedPostRowView = {
  item: CapturedPostItem;
  selected: boolean;
  selectable: boolean;
  preview: string;
  externalUrl: string | null;
};

export type UseCredentialCapturerResult = {
  activeProfileId: string;
  canDeleteProfile: boolean;
  copyPatchLabel: string;
  endpointRows: EndpointRowView[];
  globalStatusOk: boolean;
  isApplyingProfile: boolean;
  isBulkTesting: boolean;
  isUploadingCapturedPosts: boolean;
  patchOutput: string;
  profileHint: string;
  profiles: ProfileRecord[];
  sensitiveHint: string;
  settings: PopupSettings;
  testAllStatus: string;
  uploadStatus: string;
  capturedPostsStore: CapturedPostsStore | null;
  capturedPostRows: CapturedPostRowView[];
  selectedCapturedPostIds: string[];
  capturedPostsSearchQuery: string;
  captureHashtagDraft: string;
  captureHashtags: string[];
  usernameDraft: string;
  usernameHint: string;
  hashtagDraft: string;
  hashtagHint: string;
  onClearState: () => void;
  onCopyEndpoint: (model: EndpointModel, endpointId: string) => void;
  onCopyPatch: () => void;
  onCreateProfile: () => void;
  onDeleteProfile: () => void;
  onMaskSensitiveChange: (checked: boolean) => void;
  onCapturedPostsViewChange: (value: "list" | "grid") => void;
  onCapturedPostsGridColumnsChange: (value: number) => void;
  onCapturedPostsShowThumbnailChange: (value: boolean) => void;
  onOpenEndpointUrl: (url: string, openInBackground: boolean) => void;
  onProfileChange: (profileId: string) => void;
  onRefreshCookies: () => void;
  onRollbackEndpoint: (endpointId: string) => void;
  onRunAllTests: () => void;
  onToggleCapturedPost: (id: string, checked: boolean) => void;
  onSelectAllPendingCapturedPosts: () => void;
  onClearCapturedPostSelection: () => void;
  onUploadSelectedCapturedPosts: () => void;
  onClearUploadedCapturedPosts: () => void;
  onExportCapturedPosts: () => void;
  onImportCapturedPosts: (file: File) => void;
  onUploadApiBaseUrlChange: (value: string) => void;
  onUploadUserIdChange: (value: string) => void;
  onUploadOriginChange: (value: string) => void;
  onCapturedPostsSearchQueryChange: (value: string) => void;
  onCaptureHashtagDraftChange: (value: string) => void;
  onCaptureHashtagDraftKeyDown: (event: { key: string; preventDefault: () => void }) => void;
  onAddCaptureHashtag: () => void;
  onOpenCapturedPostExternalUrl: (id: string) => void;
  onOpenCaptureHashtag: (value: string) => void;
  onOpenCaptureHashtagInWindow: (value: string) => void;
  onRemoveCaptureHashtag: (value: string) => void;
  onTestEndpoint: (endpoint: EndpointDefinition) => void;
  onUsernameChange: (value: string) => void;
  onUsernameCommit: () => void;
  onHashtagChange: (value: string) => void;
  onHashtagCommit: () => void;
};

export type StorageProfileStore = {
  activeProfileId: string;
  profiles: Record<string, ProfileRecord>;
};

export type StateRef = {
  captureState: CaptureState | null;
};
