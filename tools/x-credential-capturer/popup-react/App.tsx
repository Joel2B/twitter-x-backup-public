import { ActionsBar } from "./components/actions-bar.js";
import { EndpointTable } from "./components/endpoint-table.js";
import { GlobalSection } from "./components/global-section.js";
import { PatchOutput } from "./components/patch-output.js";
import { useCredentialCapturer } from "./use-credential-capturer.js";

export default function App() {
  const {
    activeProfileId,
    canDeleteProfile,
    copyPatchLabel,
    endpointRows,
    globalStatusOk,
    isApplyingProfile,
    isBulkTesting,
    patchOutput,
    profileHint,
    profiles,
    sensitiveHint,
    settings,
    testAllStatus,
    usernameDraft,
    usernameHint,
    onClearState,
    onCopyEndpoint,
    onCopyPatch,
    onCreateProfile,
    onDeleteProfile,
    onMaskSensitiveChange,
    onOpenEndpointUrl,
    onProfileChange,
    onRefreshCookies,
    onRollbackEndpoint,
    onRunAllTests,
    onTestEndpoint,
    onUsernameChange,
    onUsernameCommit
  } = useCredentialCapturer();

  return (
    <main className="container">
      <header className="header">
        <h1>X Credential Capturer</h1>
        <p className="subtitle">Endpoint checklist + shortcuts to trigger requests.</p>
      </header>

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
        maskSensitive={settings.maskSensitive}
        onClearState={onClearState}
        onCreateProfile={onCreateProfile}
        onDeleteProfile={onDeleteProfile}
        onMaskSensitiveChange={onMaskSensitiveChange}
        onProfileChange={onProfileChange}
        onRefreshCookies={onRefreshCookies}
        onUsernameChange={onUsernameChange}
        onUsernameCommit={onUsernameCommit}
      />

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

      <PatchOutput patchOutput={patchOutput} />
    </main>
  );
}
