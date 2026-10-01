import { useRef, type ReactNode } from 'react';
import { Button, type ButtonProps } from './button';

/**
 * A styled button that opens the file picker: never the browser's "Choose File / No file chosen" control. The real input
 * is hidden and carries `inputLabel`, so tests and assistive technology can still reach it. The button's own name is its
 * visible text; pass `buttonLabel` only for an icon-only button, or to add context that still contains the visible text
 * (WCAG 2.5.3, label in name).
 */
export function FileButton({
  accept,
  onFile,
  inputLabel,
  buttonLabel,
  children,
  disabled,
  variant = 'outline',
  size = 'sm',
  className,
}: {
  accept: string;
  onFile: (file: File) => void;
  inputLabel: string;
  buttonLabel?: string;
  children: ReactNode;
  disabled?: boolean;
  variant?: ButtonProps['variant'];
  size?: ButtonProps['size'];
  className?: string;
}) {
  const input = useRef<HTMLInputElement>(null);

  return (
    <>
      <Button variant={variant} size={size} className={className} disabled={disabled} aria-label={buttonLabel} onClick={() => input.current?.click()}>
        {children}
      </Button>
      <input
        ref={input}
        type="file"
        accept={accept}
        aria-label={inputLabel}
        disabled={disabled}
        tabIndex={-1}
        hidden
        onChange={(event) => {
          const file = event.target.files?.[0];
          if (file) onFile(file);
          // Cleared, so choosing the same file again (after a failure) still fires.
          event.target.value = '';
        }}
      />
    </>
  );
}
