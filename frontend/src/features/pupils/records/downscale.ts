/** Spec 9.6: a pupil photograph is downscaled to 800 px on the long edge before upload. */
export const PHOTO_MAX_EDGE = 800;

/** A document scan keeps enough detail to read a certificate's small print (human ruling 2026-09-26). */
export const SCAN_MAX_EDGE = 2000;

const SCALABLE = new Set(['image/jpeg', 'image/png']);

/**
 * Shrinks a JPEG or PNG so its long edge is at most `maxEdge`, re-encoding as JPEG over white (a phone photograph costs a
 * clerk's data allowance otherwise). EXIF orientation is honoured, and the re-encode drops the metadata before it leaves
 * the device; the server strips it again regardless. Anything else (a PDF), an image already small enough, or a browser
 * without the canvas APIs gets the file back untouched: the server's own limits still apply.
 */
export async function downscaleImage(file: File, maxEdge: number): Promise<File> {
  if (!SCALABLE.has(file.type) || typeof createImageBitmap !== 'function' || typeof document === 'undefined') return file;

  let bitmap: ImageBitmap;
  try {
    bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
  } catch {
    return file; // Not decodable here; the server's magic-byte check gives the real answer.
  }

  const longEdge = Math.max(bitmap.width, bitmap.height);
  if (longEdge <= maxEdge) {
    bitmap.close();
    return file;
  }

  const scale = maxEdge / longEdge;
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(bitmap.width * scale);
  canvas.height = Math.round(bitmap.height * scale);
  const context = canvas.getContext('2d');
  if (!context) {
    bitmap.close();
    return file;
  }

  context.fillStyle = '#ffffff';
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();

  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.85));
  if (!blob) return file;
  return new File([blob], file.name.replace(/\.[^.]*$/, '') + '.jpg', { type: 'image/jpeg' });
}
