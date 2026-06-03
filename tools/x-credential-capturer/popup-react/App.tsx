import { useMemo, useState } from "react";

import { ActionsBar } from "./components/actions-bar.js";
import { CapturedPostsPanel } from "./components/captured-posts-panel.js";
import { EndpointTable } from "./components/endpoint-table.js";
import { GlobalSection } from "./components/global-section.js";
import { InfoPanel } from "./components/info-panel.js";
import { NotificationsPanel } from "./components/notifications-panel.js";
import { PatchOutput } from "./components/patch-output.js";
import { PopupTabNav, type PopupTabId } from "./components/popup-tab-nav.js";
import { useCredentialCapturer } from "./use-credential-capturer.js";

export default function App() {
  const {
    activeProfileId,
    canDeleteProfile,
    copyPatchLabel,
    endpointRows,
    capturedPostRows,
    capturedPostsStore,
    globalStatusOk,
    isApplyingProfile,
    isBulkTesting,
    isUploadingCapturedPosts,
    patchOutput,
    profileHint,
    profiles,
    sensitiveHint,
    settings,
    testAllStatus,
    uploadStatus,
    uploadNotifications,
    runningUploadNotificationsCount,
    selectedCapturedPostIds,
    capturedPostsSearchQuery,
    captureHashtagDraft,
    captureHashtags,
    hashtagDraft,
    hashtagHint,
    usernameDraft,
    usernameHint,
    onClearState,
    onCopyEndpoint,
    onCopyPatch,
    onCreateProfile,
    onDeleteProfile,
    onMaskSensitiveChange,
    onCapturedPostsViewChange,
    onCapturedPostsGridColumnsChange,
    onCapturedPostsShowThumbnailChange,
    onCapturedPostsSortChange,
    onOpenEndpointUrl,
    onProfileChange,
    onRefreshCookies,
    onRollbackEndpoint,
    onRunAllTests,
    onToggleCapturedPost,
    onSelectAllPendingCapturedPosts,
    onClearCapturedPostSelection,
    onUploadSelectedCapturedPosts,
    onClearUploadedCapturedPosts,
    onResetCapturedPostsUploadStatus,
    onClearUploadNotifications,
    onExportCapturedPosts,
    onImportCapturedPosts,
    onUploadApiBaseUrlChange,
    onUploadUserIdChange,
    onUploadOriginChange,
    onCapturedPostsSearchQueryChange,
    onCaptureHashtagDraftChange,
    onCaptureHashtagDraftKeyDown,
    onAddCaptureHashtag,
    onOpenCapturedPostExternalUrl,
    onOpenCaptureHashtag,
    onOpenCaptureHashtagInWindow,
    onRemoveCaptureHashtag,
    onTestEndpoint,
    onUsernameChange,
    onUsernameCommit,
    onHashtagChange,
    onHashtagCommit
  } = useCredentialCapturer();

  const [activeTab, setActiveTab] = useState<PopupTabId>("config");

  const pendingCapturedCount = useMemo(
    () => capturedPostRows.filter((row) => !row.item.uploadedAt).length,
    [capturedPostRows]
  );

  return (
    <main className="container">
      <header className="header">
        <h1>X Credential Capturer</h1>
        <p className="subtitle">Endpoint checklist + shortcuts to trigger requests.</p>
      </header>

      <PopupTabNav
        activeTab={activeTab}
        pendingCapturedCount={pendingCapturedCount}
        runningUploadNotificationsCount={runningUploadNotificationsCount}
        onTabChange={setActiveTab}
      />

      {activeTab === "config" && (
        <GlobalSection
          activeProfileId={activeProfileId}
          canDeleteProfile={canDeleteProfile}
          globalStatusOk={globalStatusOk}
          isApplyingProfile={isApplyingProfile}
          profileHint={profileHint}
          profiles={profiles}
          sensitiveHint={sensitiveHint}
          usernameDraft={usernameDraft}
          usernameHint={usernameHint}
          hashtagDraft={hashtagDraft}
          hashtagHint={hashtagHint}
          maskSensitive={settings.maskSensitive}
          onClearState={onClearState}
          onCreateProfile={onCreateProfile}
          onDeleteProfile={onDeleteProfile}
          onMaskSensitiveChange={onMaskSensitiveChange}
          onProfileChange={onProfileChange}
          onRefreshCookies={onRefreshCookies}
          onUsernameChange={onUsernameChange}
          onUsernameCommit={onUsernameCommit}
          onHashtagChange={onHashtagChange}
          onHashtagCommit={onHashtagCommit}
        />
      )}

      {activeTab === "endpoints" && (
        <>
          <EndpointTable
            endpointRows={endpointRows}
            onCopyEndpoint={onCopyEndpoint}
            onOpenEndpointUrl={onOpenEndpointUrl}
            onRollbackEndpoint={onRollbackEndpoint}
            onTestEndpoint={onTestEndpoint}
          />

          <ActionsBar
            copyPatchLabel={copyPatchLabel}
            isBulkTesting={isBulkTesting}
            isMaskSensitive={settings.maskSensitive}
            testAllStatus={testAllStatus}
            onCopyPatch={onCopyPatch}
            onRunAllTests={onRunAllTests}
          />
        </>
      )}

      {activeTab === "captured-posts" && (
        <CapturedPostsPanel
          apiBaseUrl={capturedPostsStore?.apiBaseUrl || ""}
          uploadUserId={capturedPostsStore?.uploadUserId || ""}
          uploadOrigin={capturedPostsStore?.uploadOrigin || ""}
          uploadStatus={uploadStatus}
          isUploading={isUploadingCapturedPosts}
          viewMode={settings.capturedPostsView}
          gridColumns={settings.capturedPostsGridColumns}
          showThumbnail={settings.capturedPostsShowThumbnail}
          sortOrder={settings.capturedPostsSort}
          rows={capturedPostRows}
          selectedCount={selectedCapturedPostIds.length}
          capturedPostsSearchQuery={capturedPostsSearchQuery}
          captureHashtagDraft={captureHashtagDraft}
          captureHashtags={captureHashtags}
          onApiBaseUrlChange={onUploadApiBaseUrlChange}
          onUploadUserIdChange={onUploadUserIdChange}
          onUploadOriginChange={onUploadOriginChange}
          onCapturedPostsSearchQueryChange={onCapturedPostsSearchQueryChange}
          onCaptureHashtagDraftChange={onCaptureHashtagDraftChange}
          onCaptureHashtagDraftKeyDown={onCaptureHashtagDraftKeyDown}
          onAddCaptureHashtag={onAddCaptureHashtag}
          onOpenCapturedPostExternalUrl={onOpenCapturedPostExternalUrl}
          onOpenCaptureHashtag={onOpenCaptureHashtag}
          onOpenCaptureHashtagInWindow={onOpenCaptureHashtagInWindow}
          onRemoveCaptureHashtag={onRemoveCaptureHashtag}
          onToggleRow={onToggleCapturedPost}
          onSelectAllPending={onSelectAllPendingCapturedPosts}
          onClearSelection={onClearCapturedPostSelection}
          onUploadSelected={onUploadSelectedCapturedPosts}
          onClearUploaded={onClearUploadedCapturedPosts}
          onResetUploadStatus={onResetCapturedPostsUploadStatus}
          onExport={onExportCapturedPosts}
          onImport={onImportCapturedPosts}
          onViewModeChange={onCapturedPostsViewChange}
          onGridColumnsChange={onCapturedPostsGridColumnsChange}
          onShowThumbnailChange={onCapturedPostsShowThumbnailChange}
          onSortOrderChange={onCapturedPostsSortChange}
        />
      )}

      {activeTab === "notifications" && (
        <NotificationsPanel
          notifications={uploadNotifications}
          runningCount={runningUploadNotificationsCount}
          onClear={onClearUploadNotifications}
        />
      )}

      {activeTab === "patch-preview" && <PatchOutput patchOutput={patchOutput} />}

      {activeTab === "info" && <InfoPanel />}
    </main>
  );
}
