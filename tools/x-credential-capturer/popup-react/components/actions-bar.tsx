type ActionsBarProps = {
  copyPatchLabel: string;
  isBulkTesting: boolean;
  isMaskSensitive: boolean;
  testAllStatus: string;
  onCopyPatch: () => void;
  onRunAllTests: () => void;
};

export function ActionsBar({
  copyPatchLabel,
  isBulkTesting,
  isMaskSensitive,
  onCopyPatch,
  onRunAllTests,
  testAllStatus
}: ActionsBarProps) {
  return (
    <section className="actions">
      <button
        className="btn primary"
        disabled={isMaskSensitive}
        title={isMaskSensitive ? "Disable Sensitive guard to copy real credentials" : ""}
        onClick={onCopyPatch}
      >
        {copyPatchLabel}
      </button>
      <button className="btn secondary" disabled={isBulkTesting} onClick={onRunAllTests}>
        {isBulkTesting ? "Testing all..." : "Test all"}
      </button>
      <span className="meta">{testAllStatus}</span>
    </section>
  );
}
