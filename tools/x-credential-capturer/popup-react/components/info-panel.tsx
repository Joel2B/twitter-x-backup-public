import { formatDate } from "../../popup/format.js";
import { buildInfo } from "../generated/build-info.js";

export function InfoPanel() {
  return (
    <section className="info-panel">
      <div className="info-panel-header">
        <h2>Build Info</h2>
        <span className="meta">Updated on every extension build.</span>
      </div>

      <div className="info-grid">
        <span>Build version: {buildInfo.buildVersion || "-"}</span>
        <span>Built at: {buildInfo.builtAt ? formatDate(buildInfo.builtAt) : "-"}</span>
        <span>Commit: {buildInfo.gitCommit || "-"}</span>
      </div>
    </section>
  );
}
