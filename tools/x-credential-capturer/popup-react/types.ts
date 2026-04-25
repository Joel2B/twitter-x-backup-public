import type {
  CaptureState,
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

export type UseCredentialCapturerResult = {
  activeProfileId: string;
  canDeleteProfile: boolean;
  copyPatchLabel: string;
  endpointRows: EndpointRowView[];
  globalStatusOk: boolean;
  isApplyingProfile: boolean;
  isBulkTesting: boolean;
  patchOutput: string;
  profileHint: string;
  profiles: ProfileRecord[];
  sensitiveHint: string;
  settings: PopupSettings;
  testAllStatus: string;
  usernameDraft: string;
  usernameHint: string;
  onClearState: () => void;
  onCopyEndpoint: (model: EndpointModel, endpointId: string) => void;
  onCopyPatch: () => void;
  onCreateProfile: () => void;
  onDeleteProfile: () => void;
  onMaskSensitiveChange: (checked: boolean) => void;
  onOpenEndpointUrl: (url: string, openInBackground: boolean) => void;
  onProfileChange: (profileId: string) => void;
  onRefreshCookies: () => void;
  onRollbackEndpoint: (endpointId: string) => void;
  onRunAllTests: () => void;
  onTestEndpoint: (endpoint: EndpointDefinition) => void;
  onUsernameChange: (value: string) => void;
  onUsernameCommit: () => void;
};

export type StorageProfileStore = {
  activeProfileId: string;
  profiles: Record<string, ProfileRecord>;
};

export type StateRef = {
  captureState: CaptureState | null;
};
