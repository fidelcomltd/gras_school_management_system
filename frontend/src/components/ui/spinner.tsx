import { LoaderCircle } from 'lucide-react';
import { cn } from '@/lib/utils/cn';

/** A spinning ring for work in progress. Decorative: the caller names the wait in text or an accessible label. */
export function Spinner({ className }: { className?: string }) {
  return <LoaderCircle aria-hidden="true" className={cn('size-5 animate-spin text-primary', className)} />;
}
