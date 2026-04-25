type PatchOutputProps = {
  patchOutput: string;
};

export function PatchOutput({ patchOutput }: PatchOutputProps) {
  return (
    <section className="output">
      <label htmlFor="patchOutput">Patch preview:</label>
      <textarea id="patchOutput" value={patchOutput} readOnly />
    </section>
  );
}
