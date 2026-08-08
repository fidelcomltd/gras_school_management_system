interface Swatch {
  token: string;
  className: string;
  note: string;
}

/**
 * Every pair here is a real semantic token from `styles/semantic.css`, rendered
 * through Tailwind utilities. If a swatch looks wrong in either mode, the token
 * is wrong — not this file.
 */
const SEMANTIC: readonly Swatch[] = [
  { token: 'primary', className: 'bg-primary text-primary-foreground', note: 'Main actions, active nav' },
  { token: 'primary-subtle', className: 'bg-primary-subtle text-primary-subtle-foreground', note: 'Tinted fill' },
  { token: 'secondary', className: 'bg-secondary text-secondary-foreground', note: 'Quiet action' },
  { token: 'accent', className: 'bg-accent text-accent-foreground', note: 'Crest gold — highlights' },
  { token: 'surface', className: 'bg-surface text-surface-foreground border border-border', note: 'Cards, panels' },
  { token: 'surface-sunken', className: 'bg-surface-sunken text-foreground border border-border', note: 'Wells, table heads' },
  { token: 'muted', className: 'bg-muted text-muted-foreground', note: 'De-emphasised' },
  { token: 'success', className: 'bg-success text-success-foreground', note: 'Confirmed outcome' },
  { token: 'warning', className: 'bg-warning text-warning-foreground', note: 'Needs attention' },
  { token: 'info', className: 'bg-info text-info-foreground', note: 'Neutral notice' },
  { token: 'destructive', className: 'bg-destructive text-destructive-foreground', note: 'Irreversible action' },
];

const BRAND: readonly { name: string; steps: readonly string[] }[] = [
  { name: 'royal', steps: ['bg-royal-100', 'bg-royal-300', 'bg-royal-500', 'bg-royal-700', 'bg-royal-900'] },
  { name: 'gold', steps: ['bg-gold-100', 'bg-gold-300', 'bg-gold-500', 'bg-gold-700', 'bg-gold-900'] },
  { name: 'crimson', steps: ['bg-crimson-100', 'bg-crimson-300', 'bg-crimson-500', 'bg-crimson-700', 'bg-crimson-900'] },
];

export function TokenSwatches() {
  return (
    <section aria-labelledby="tokens-heading" className="flex flex-col gap-6">
      <div>
        <h2 id="tokens-heading" className="font-display text-xl font-semibold text-foreground">
          Colour tokens
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Semantic tokens are the only colours components may use. Toggle the theme — every
          swatch below re-resolves without a single <code className="font-mono text-xs">dark:</code> class.
        </p>
      </div>

      <ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
        {SEMANTIC.map((swatch) => (
          <li key={swatch.token} className={`rounded-lg p-4 ${swatch.className}`}>
            <p className="font-mono text-sm font-medium">{swatch.token}</p>
            <p className="mt-1 text-xs opacity-80">{swatch.note}</p>
          </li>
        ))}
      </ul>

      <div>
        <h3 className="text-sm font-semibold text-foreground">Brand ramps</h3>
        <p className="mt-1 text-xs text-muted-foreground">
          Derived from the crest. Available for decorative chrome; prefer the semantic tokens above.
        </p>
        <div className="mt-3 flex flex-col gap-2">
          {BRAND.map((ramp) => (
            <div key={ramp.name} className="flex items-center gap-3">
              <span className="w-16 font-mono text-xs text-muted-foreground">{ramp.name}</span>
              <div className="flex flex-1 overflow-hidden rounded-md border border-border">
                {ramp.steps.map((step) => (
                  <span key={step} className={`h-8 flex-1 ${step}`} />
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
