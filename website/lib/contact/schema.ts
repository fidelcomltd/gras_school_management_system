import { enquiryTopics } from "@/content/school"

/**
 * One definition of a valid enquiry, used by the browser (instant field errors) and by /api/contact
 * (the authority). Rules from the design's handoff notes: name, phone, message and consent required;
 * phone is a Nigerian number (with or without 0 / +234); message at most 300 characters.
 *
 * Plain TypeScript on purpose: this ships to the browser, and a schema library cost ~90 KB gzipped
 * for five fields.
 */
export const MESSAGE_MAX = 300

export type ContactField = "name" | "phone" | "email" | "message" | "consent"
export type EnquiryTopic = (typeof enquiryTopics)[number]

export type ContactInput = {
  name: string
  phone: string
  email: string
  topic: EnquiryTopic
  message: string
  consent: boolean
  /** Honeypot: a field people never see. Bots fill it in; the API then drops the enquiry silently. */
  website?: string
  /** ms between the form appearing and submit, measured in the browser: faster than a human is dropped. */
  elapsedMs?: number
}

export type Validated =
  | { ok: true; data: Omit<ContactInput, "consent"> & { consent: true } }
  | { ok: false; fields: ContactField[] }

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/
const digitsOnly = (v: string) => v.replace(/\D/g, "")

/**
 * The national significant number (10 digits, no leading 0) of a Nigerian phone typed any common way:
 * "0813 647 5557", "813 647 5557", "+234 813 647 5557", "2348136475557". Undefined if it isn't one.
 */
export function nationalNumber(raw: string): string | undefined {
  let d = digitsOnly(raw)
  if (d.length === 13 && d.startsWith("234")) d = d.slice(3)
  else if (d.length === 11 && d.startsWith("0")) d = d.slice(1)
  return d.length === 10 && !d.startsWith("0") ? d : undefined
}
const str = (v: unknown) => (typeof v === "string" ? v.trim() : "")

/** Validates untrusted input (a JSON body, or the form's state) and returns trimmed data or the bad fields. */
export function validateEnquiry(input: unknown): Validated {
  const o = (input && typeof input === "object" ? input : {}) as Record<string, unknown>
  const name = str(o.name)
  const phone = str(o.phone)
  const email = str(o.email)
  const message = str(o.message)
  const topic = (enquiryTopics as readonly string[]).includes(o.topic as string)
    ? (o.topic as EnquiryTopic)
    : enquiryTopics[0]

  const fields: ContactField[] = []
  if (name.length < 2 || name.length > 100) fields.push("name")
  if (!nationalNumber(phone)) fields.push("phone")
  if (email !== "" && (email.length > 200 || !EMAIL.test(email))) fields.push("email")
  if (message.length < 2 || message.length > MESSAGE_MAX) fields.push("message")
  if (o.consent !== true) fields.push("consent")
  if (fields.length) return { ok: false, fields }

  return {
    ok: true,
    data: {
      name,
      phone,
      email,
      topic,
      message,
      consent: true,
      website: typeof o.website === "string" ? o.website.slice(0, 500) : undefined,
      elapsedMs: typeof o.elapsedMs === "number" && Number.isFinite(o.elapsedMs) ? o.elapsedMs : undefined,
    },
  }
}

/** Field names that failed validation. */
export function invalidFields(input: unknown): ContactField[] {
  const r = validateEnquiry(input)
  return r.ok ? [] : r.fields
}

/** Any accepted form → "+2348130000000". Call only on a validated number. */
export function toInternationalPhone(raw: string): string {
  return `+234${nationalNumber(raw) ?? digitsOnly(raw)}`
}
