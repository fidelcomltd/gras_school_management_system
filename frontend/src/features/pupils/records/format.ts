import { ApiError } from '@/lib/http';
import { FileTooLargeError } from './downscale';

/** A save failure as the office should read it: a 422's per-field reasons, which its generic message hides. */
export function errorText(error: unknown): string | null {
  if (error instanceof FileTooLargeError) return error.message;
  if (!(error instanceof ApiError)) return null;
  const reasons = error.fieldErrors ? Object.values(error.fieldErrors).flat() : [];
  return reasons.length > 0 ? reasons.join(' ') : error.message;
}

/** "+2348031234567" (as stored) back to the form people type, "08031234567". */
export const localPhone = (phone: string | null | undefined) => (phone ? phone.replace(/^\+234/, '0') : '');
