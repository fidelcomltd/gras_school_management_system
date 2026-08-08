import { Input as BaseInput } from '@base-ui/react/input';
import { cva, type VariantProps } from 'class-variance-authority';
import type { ComponentPropsWithRef } from 'react';
import { cn } from '@/lib/utils/cn';
import type { WithClassName } from './props';

export const inputVariants = cva(
  [
    'flex w-full rounded-md border border-input bg-surface px-3 text-foreground',
    'transition-colors placeholder:text-muted-foreground',
    'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
    'disabled:cursor-not-allowed disabled:opacity-60',
    // Base UI sets data-invalid from the enclosing Field when validation fails.
    'data-invalid:border-destructive data-invalid:focus-visible:outline-destructive',
  ],
  {
    variants: {
      size: {
        sm: 'h-8 text-sm',
        md: 'h-10 text-sm',
        lg: 'h-11 text-base',
      },
    },
    defaultVariants: { size: 'md' },
  },
);

export interface InputProps
  // The native `size` attribute is a number and collides with our variant, so
  // it is dropped. Use a width utility class instead.
  extends WithClassName<Omit<ComponentPropsWithRef<typeof BaseInput>, 'size'>>,
    VariantProps<typeof inputVariants> {}

export function Input({ className, size, ...props }: InputProps) {
  return <BaseInput className={cn(inputVariants({ size }), className)} {...props} />;
}
