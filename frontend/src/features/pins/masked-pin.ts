/** The one visible form of a pin after printing (spec 6.8): its prefix, the rest shown as hidden. */
export const HIDDEN_REST = '••••••';

/** For screen readers and titles: says the rest is hidden rather than reading out bullets. */
export const pinLabel = (prefix: string) => `pin starting ${prefix}`;
