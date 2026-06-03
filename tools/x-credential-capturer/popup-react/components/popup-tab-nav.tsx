export type PopupTabId =
  | "config"
  | "endpoints"
  | "captured-posts"
  | "notifications"
  | "patch-preview"
  | "info";

type PopupTabNavProps = {
  activeTab: PopupTabId;
  pendingCapturedCount: number;
  runningUploadNotificationsCount: number;
  onTabChange: (tab: PopupTabId) => void;
};

type TabButtonProps = {
  activeTab: PopupTabId;
  tab: PopupTabId;
  label: string;
  onTabChange: (tab: PopupTabId) => void;
};

function TabButton({ activeTab, tab, label, onTabChange }: TabButtonProps) {
  return (
    <button
      className={`tab-btn ${activeTab === tab ? "active" : ""}`}
      type="button"
      onClick={() => {
        onTabChange(tab);
      }}
    >
      {label}
    </button>
  );
}

export function PopupTabNav({
  activeTab,
  pendingCapturedCount,
  runningUploadNotificationsCount,
  onTabChange
}: PopupTabNavProps) {
  return (
    <nav className="tab-nav">
      <TabButton activeTab={activeTab} tab="config" label="Config" onTabChange={onTabChange} />
      <TabButton
        activeTab={activeTab}
        tab="endpoints"
        label="Endpoints"
        onTabChange={onTabChange}
      />
      <TabButton
        activeTab={activeTab}
        tab="captured-posts"
        label={`Captured Posts ${pendingCapturedCount > 0 ? `(${pendingCapturedCount})` : ""}`}
        onTabChange={onTabChange}
      />
      <TabButton
        activeTab={activeTab}
        tab="notifications"
        label={`Notifications ${runningUploadNotificationsCount > 0 ? `(${runningUploadNotificationsCount})` : ""}`}
        onTabChange={onTabChange}
      />
      <TabButton
        activeTab={activeTab}
        tab="patch-preview"
        label="Patch Preview"
        onTabChange={onTabChange}
      />
      <TabButton activeTab={activeTab} tab="info" label="Info" onTabChange={onTabChange} />
    </nav>
  );
}
