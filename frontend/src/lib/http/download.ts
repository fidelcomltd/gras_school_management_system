import { isAxiosError } from 'axios';
import { terminateSession } from '@/lib/auth/auth-session';
import { httpClient } from './http-client';
import { normalizeError } from './http-error';

/** A file the API returned, ready to hand to the browser. */
export interface DownloadedFile {
  blob: Blob;
  fileName: string;
}

function fileNameFrom(disposition: unknown): string | null {
  if (typeof disposition !== 'string') return null;
  const star = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (star?.[1]) return decodeURIComponent(star[1]);
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain?.[1] ?? null;
}

/**
 * GETs a file (a PDF) for endpoints that write on read (printing pin slips moves the batch to Printed), so they must be
 * fetched and saved, never opened by navigation. Read as an ArrayBuffer and wrapped in a Blob here, which behaves the
 * same in every browser and in the test environment. Errors come back as `ApiError` like every other helper; a problem
 * document delivered as bytes is decoded first so its message survives.
 */
export async function getFile(url: string, fallbackName: string): Promise<DownloadedFile> {
  try {
    const response = await httpClient.get<ArrayBuffer>(url, { responseType: 'arraybuffer' });
    const type = typeof response.headers['content-type'] === 'string' ? response.headers['content-type'] : 'application/octet-stream';
    return {
      blob: new Blob([response.data], { type }),
      fileName: fileNameFrom(response.headers['content-disposition']) ?? fallbackName,
    };
  } catch (error) {
    if (isAxiosError(error) && error.response?.data instanceof ArrayBuffer) {
      try {
        error.response.data = JSON.parse(new TextDecoder().decode(error.response.data)) as unknown;
      } catch {
        // Not JSON: normalizeError falls back to the status text.
      }
    }
    const apiError = normalizeError(error);
    if (apiError.endsSession) terminateSession();
    throw apiError;
  }
}

/** Hands a downloaded file to the browser's save dialog. */
export function saveFile(file: DownloadedFile): void {
  const href = URL.createObjectURL(file.blob);
  const link = document.createElement('a');
  link.href = href;
  link.download = file.fileName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(href), 0);
}
