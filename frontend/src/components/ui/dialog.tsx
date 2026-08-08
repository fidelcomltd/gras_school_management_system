import { Dialog as BaseDialog } from '@base-ui/react/dialog';
import type { ComponentPropsWithRef, ReactNode } from 'react';
import { cn } from '@/lib/utils/cn';
import { CloseIcon } from './icons';
import type { WithClassName } from './props';

/**
 * Modal dialog over Base UI. Focus trapping, scroll locking, `Escape` to
 * dismiss, and the `aria-labelledby`/`aria-describedby` wiring all come from
 * the primitive — `DialogTitle` and `DialogDescription` are what feed it, so
 * always render a `DialogTitle`.
 */

export const Dialog = BaseDialog.Root;
export const DialogTrigger = BaseDialog.Trigger;
export const DialogClose = BaseDialog.Close;

export interface DialogContentProps
  extends WithClassName<ComponentPropsWithRef<typeof BaseDialog.Popup>> {
  /** Renders the corner dismiss button. Turn off for flows that must be resolved. */
  showCloseButton?: boolean | undefined;
  children?: ReactNode | undefined;
}

export function DialogContent({
  className,
  showCloseButton = true,
  children,
  ...props
}: DialogContentProps) {
  return (
    <BaseDialog.Portal>
      <BaseDialog.Backdrop
        className={cn(
          'fixed inset-0 z-50 bg-overlay backdrop-blur-[2px]',
          'transition-opacity duration-200',
          'data-starting-style:opacity-0 data-ending-style:opacity-0',
        )}
      />
      <BaseDialog.Popup
        className={cn(
          'fixed top-1/2 left-1/2 z-50 w-[calc(100vw-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2',
          'rounded-xl border border-border bg-surface-raised p-6 text-surface-foreground shadow-xl',
          'transition-[opacity,transform] duration-200',
          'data-starting-style:scale-95 data-starting-style:opacity-0',
          'data-ending-style:scale-95 data-ending-style:opacity-0',
          className,
        )}
        {...props}
      >
        {children}
        {showCloseButton ? (
          <BaseDialog.Close
            aria-label="Close dialog"
            className={cn(
              'absolute top-4 right-4 inline-flex size-8 items-center justify-center rounded-md',
              'text-muted-foreground transition-colors hover:bg-muted hover:text-foreground',
              'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
            )}
          >
            <CloseIcon className="size-4" />
          </BaseDialog.Close>
        ) : null}
      </BaseDialog.Popup>
    </BaseDialog.Portal>
  );
}

export function DialogHeader({ className, ...props }: ComponentPropsWithRef<'div'>) {
  return <div className={cn('mb-4 flex flex-col gap-1.5 pr-8', className)} {...props} />;
}

export function DialogFooter({ className, ...props }: ComponentPropsWithRef<'div'>) {
  return (
    <div
      className={cn('mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end', className)}
      {...props}
    />
  );
}

export function DialogTitle({
  className,
  ...props
}: WithClassName<ComponentPropsWithRef<typeof BaseDialog.Title>>) {
  return (
    <BaseDialog.Title
      className={cn('font-display text-lg font-semibold text-foreground', className)}
      {...props}
    />
  );
}

export function DialogDescription({
  className,
  ...props
}: WithClassName<ComponentPropsWithRef<typeof BaseDialog.Description>>) {
  return (
    <BaseDialog.Description className={cn('text-sm text-muted-foreground', className)} {...props} />
  );
}
