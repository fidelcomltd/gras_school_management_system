import { useId, useRef, type ReactNode } from 'react';
import { Button, type ButtonProps } from './button';

/**
 * A styled button that opens the file picker: never the browser's "Choose File / No file chosen" control. The real input
 * is hidden but labelled with the button's text, so it stays reachable for tests and assistive technology alike.
 */
export function FileButton({
  accept,
  onFile,
  label,
  children,
  disabled,
  variant = 'outline',
  size = 'sm',
  className,
}: {
  accept: string;
  onFile: (file: File) => void;
  /** The accessible name, when `children` is an icon or not the full sentence. */
  label: string;
  children: ReactNode;
  disabled?: boolean;
  variant?: ButtonProps['variant'];
  size?: ButtonProps['size'];
  className?: string;
}) {
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);

  return (
    <>
      <Button variant={variant} size={size} className={className} disabled={disabled} aria-label={label} onClick={() => input.current?.click()}>
        {children}
      </Button>
      <input
        id={inputId}
        ref={input}
        type="file"
        accept={accept}
        aria-label={label}
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
