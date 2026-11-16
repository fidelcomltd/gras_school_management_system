"use client"

import { useEffect, useId, useRef, useState, type FormEvent } from "react"
import { site } from "@/config/site"
import { contactForm as copy } from "@/content/contact"
import { enquiryTopics } from "@/content/school"
import { invalidFields, MESSAGE_MAX, type ContactField } from "@/lib/contact/schema"
import { cn } from "@/lib/cn"
import { Icon } from "@/components/ui/Icon"

type Values = {
  name: string
  phone: string
  email: string
  topic: (typeof enquiryTopics)[number]
  message: string
  consent: boolean
}
const empty: Values = { name: "", phone: "", email: "", topic: enquiryTopics[0], message: "", consent: false }
type Status = "idle" | "sending" | "sent" | "failed"

const label = "text-sm font-semibold text-navy"
const fieldBase =
  "w-full rounded-[14px] border-[1.5px] bg-field text-base text-ink transition-[border-color,box-shadow] duration-200 placeholder:text-muted focus:border-royal focus:shadow-focus focus:outline-none"
const errorText = "text-[13px] font-medium text-crimson"

/** "Send us a message" — posts to /api/contact, which emails the school through Resend. */
export function ContactForm() {
  const [values, setValues] = useState<Values>(empty)
  const [tried, setTried] = useState(false)
  const [status, setStatus] = useState<Status>("idle")
  const [sentName, setSentName] = useState("")
  const startedAt = useRef(0)
  const formRef = useRef<HTMLFormElement>(null)
  const successRef = useRef<HTMLHeadingElement>(null)
  const uid = useId()
  const id = (f: string) => `${uid}-${f}`

  // Started once per fresh form (mount, and "Send another message"); a retry after a failed send
  // keeps the original start, so a quick retry is never mistaken for a bot.
  useEffect(() => {
    startedAt.current = Date.now()
  }, [])

  useEffect(() => {
    if (status === "sent") successRef.current?.focus()
  }, [status])

  const bad = new Set(invalidFields(values))
  const show = (f: ContactField) => tried && bad.has(f)
  const set = <K extends keyof Values>(k: K, v: Values[K]) => setValues((prev) => ({ ...prev, [k]: v }))

  async function onSubmit(e: FormEvent) {
    e.preventDefault()
    if (status === "sending") return
    if (bad.size > 0) {
      setTried(true)
      const first = (["name", "phone", "email", "message", "consent"] as const).find((f) => bad.has(f))
      if (first) formRef.current?.querySelector<HTMLElement>(`#${CSS.escape(id(first))}`)?.focus()
      return
    }
    setStatus("sending")
    const honeypot = (formRef.current?.elements.namedItem("website") as HTMLInputElement | null)?.value ?? ""
    try {
      const res = await fetch("/api/contact", {
        method: "POST",
        headers: { "content-type": "application/json" },
        // Elapsed time on the browser's own clock: comparing it with the server's would drop real
        // enquiries from phones whose clocks run fast.
        body: JSON.stringify({ ...values, website: honeypot, elapsedMs: Date.now() - startedAt.current }),
      })
      if (!res.ok) throw new Error(String(res.status))
      setSentName(values.name.trim().split(/\s+/)[0] || "there")
      setStatus("sent")
      setValues(empty)
      setTried(false)
    } catch {
      setStatus("failed")
    }
  }

  if (status === "sent") {
    return (
      <div
        role="status"
        className="flex min-h-[420px] animate-pop flex-col items-center justify-center gap-4 text-center md:min-h-[560px] md:gap-[18px]"
      >
        <span className="flex size-20 items-center justify-center rounded-full bg-navy shadow-[0_0_0_8px_var(--color-sky-tint)] md:size-[92px] md:shadow-[0_0_0_10px_var(--color-sky-tint)]">
          <Icon name="check" size={42} strokeWidth={2.6} className="size-9 text-gold md:size-[42px]" />
        </span>
        <h2 ref={successRef} tabIndex={-1} className="text-[26px] font-extrabold text-navy outline-none md:text-[32px]">
          {copy.success.title(sentName)}
        </h2>
        <p className="max-w-[420px] text-[15.5px] leading-[1.6] text-slate md:text-[17px] md:leading-[1.65]">
          {copy.success.text}
        </p>
        <button
          type="button"
          onClick={() => {
            startedAt.current = Date.now()
            setStatus("idle")
          }}
          className="mt-1.5 h-[50px] lift-on-hover cursor-pointer rounded-full border-[1.5px] border-navy bg-white px-6 text-[15px] font-semibold text-navy hover:bg-navy hover:text-white md:mt-2 md:h-[52px] md:px-[26px]"
        >
          {copy.success.again}
        </button>
      </div>
    )
  }

  const count = values.message.length
  return (
    <form
      ref={formRef}
      onSubmit={onSubmit}
      noValidate
      aria-labelledby={id("title")}
      className="flex flex-col gap-[18px] md:gap-[22px]"
    >
      <div className="flex flex-col gap-1.5 md:gap-2">
        <h2 id={id("title")} className="text-2xl font-extrabold text-navy md:text-[30px]">
          {copy.title}
        </h2>
        <p className="text-sm leading-[1.55] text-slate md:text-[15.5px] md:leading-[1.6]">
          {copy.required.before} <span className="font-bold text-crimson">*</span> {copy.required.after}
          <span className="hidden md:inline"> {copy.replyNote}</span>
        </p>
      </div>

      {tried && bad.size > 0 && (
        <div
          role="alert"
          className="flex items-start gap-2.5 rounded-xl border border-error-line bg-crimson-tint px-3.5 py-3 text-sm leading-normal text-crimson-hover md:gap-3 md:rounded-[14px] md:px-4 md:py-3.5 md:text-[14.5px]"
        >
          <Icon name="alert" size={20} className="mt-0.5 size-[18px] shrink-0 md:mt-px md:size-5" />
          <span>
            <span className="md:hidden">{copy.mobileErrorSummary}</span>
            <span className="hidden md:inline">{copy.errorSummary}</span>
          </span>
        </div>
      )}
      {status === "failed" && (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-[14px] border border-error-line bg-crimson-tint px-4 py-3.5 text-[14.5px] leading-normal text-crimson-hover"
        >
          <Icon name="alert" size={20} className="mt-px shrink-0" />
          <span>
            {copy.sendFailed}{" "}
            <a href={site.contact.phoneHref} className="font-semibold underline">
              {site.contact.phoneDisplay}
            </a>
            .
          </span>
        </div>
      )}

      <div className="grid gap-[18px] md:grid-cols-2 md:gap-5">
        <div className="flex flex-col gap-2">
          <label htmlFor={id("name")} className={label}>
            {copy.fields.name.label} <span className="text-crimson">*</span>
          </label>
          <input
            id={id("name")}
            name="name"
            type="text"
            autoComplete="name"
            required
            placeholder={copy.fields.name.placeholder}
            value={values.name}
            onChange={(e) => set("name", e.target.value)}
            aria-invalid={show("name")}
            aria-describedby={show("name") ? id("name-err") : undefined}
            className={cn(fieldBase, "h-[55px] px-3.5 md:px-4", show("name") ? "border-crimson" : "border-field-line")}
          />
          {show("name") && (
            <span id={id("name-err")} className={errorText}>
              {copy.fields.name.error}
            </span>
          )}
        </div>

        <div className="flex flex-col gap-2">
          <label htmlFor={id("phone")} className={label}>
            {copy.fields.phone.label} <span className="text-crimson">*</span>
          </label>
          <div
            className={cn(
              "flex h-[55px] overflow-hidden rounded-[14px] border-[1.5px] bg-field transition-[border-color,box-shadow] duration-200 focus-within:border-royal focus-within:shadow-focus",
              show("phone") ? "border-crimson" : "border-field-line"
            )}
          >
            <span
              aria-hidden="true"
              className="flex shrink-0 items-center border-r-[1.5px] border-field-line bg-field-addon px-3 text-[15px] font-semibold text-slate md:px-3.5"
            >
              +234
            </span>
            <input
              id={id("phone")}
              name="phone"
              type="tel"
              inputMode="tel"
              autoComplete="tel-national"
              required
              placeholder={copy.fields.phone.placeholder}
              value={values.phone}
              onChange={(e) => set("phone", e.target.value)}
              aria-invalid={show("phone")}
              aria-describedby={show("phone") ? id("phone-err") : undefined}
              className="min-w-0 flex-1 border-0 bg-transparent px-3 text-base text-ink placeholder:text-muted focus:outline-none md:px-3.5"
            />
          </div>
          {show("phone") && (
            <span id={id("phone-err")} className={errorText}>
              {copy.fields.phone.error}
            </span>
          )}
        </div>

        <div className="flex flex-col gap-2">
          <label htmlFor={id("email")} className={label}>
            {copy.fields.email.label} <span className="font-medium text-muted">{copy.fields.email.optional}</span>
          </label>
          <input
            id={id("email")}
            name="email"
            type="email"
            autoComplete="email"
            placeholder={copy.fields.email.placeholder}
            value={values.email}
            onChange={(e) => set("email", e.target.value)}
            aria-invalid={show("email")}
            aria-describedby={show("email") ? id("email-err") : undefined}
            className={cn(fieldBase, "h-[55px] px-3.5 md:px-4", show("email") ? "border-crimson" : "border-field-line")}
          />
          {show("email") && (
            <span id={id("email-err")} className={errorText}>
              {copy.fields.email.error}
            </span>
          )}
        </div>

        <div className="flex flex-col gap-2">
          <label htmlFor={id("topic")} className={label}>
            {copy.fields.topic.label}
          </label>
          <select
            id={id("topic")}
            name="topic"
            value={values.topic}
            onChange={(e) => set("topic", e.target.value as Values["topic"])}
            className={cn(fieldBase, "h-[52px] cursor-pointer border-field-line px-3 md:px-3.5")}
          >
            {enquiryTopics.map((t) => (
              <option key={t} value={t}>
                {t}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex flex-col gap-2">
        <div className="flex items-baseline justify-between">
          <label htmlFor={id("message")} className={label}>
            {copy.fields.message.label} <span className="text-crimson">*</span>
          </label>
          <span
            aria-live="polite"
            className={cn("text-[12.5px]", MESSAGE_MAX - count < 30 ? "text-crimson" : "text-slate")}
          >
            {count} / {MESSAGE_MAX}
          </span>
        </div>
        <textarea
          id={id("message")}
          name="message"
          rows={5}
          maxLength={MESSAGE_MAX}
          required
          placeholder={copy.fields.message.placeholder}
          value={values.message}
          onChange={(e) => set("message", e.target.value)}
          aria-invalid={show("message")}
          aria-describedby={show("message") ? id("message-err") : undefined}
          className={cn(
            fieldBase,
            "resize-y px-3.5 py-3 leading-[1.55] md:px-4 md:py-3.5",
            show("message") ? "border-crimson" : "border-field-line"
          )}
        />
        {show("message") && (
          <span id={id("message-err")} className={errorText}>
            {copy.fields.message.error}
          </span>
        )}
      </div>

      <div className="flex flex-col gap-2">
        <label className="flex cursor-pointer items-start gap-3 text-sm leading-normal text-slate-deep md:text-[14.5px]">
          <input
            id={id("consent")}
            name="consent"
            type="checkbox"
            checked={values.consent}
            onChange={(e) => set("consent", e.target.checked)}
            aria-invalid={show("consent")}
            aria-describedby={show("consent") ? id("consent-err") : undefined}
            className="m-0 size-[22px] shrink-0 cursor-pointer accent-crimson md:mt-px md:size-5"
          />
          <span>
            {copy.fields.consent.label} <span className="text-crimson">*</span>
          </span>
        </label>
        {show("consent") && (
          <span id={id("consent-err")} className={errorText}>
            {copy.fields.consent.error}
          </span>
        )}
      </div>

      {/* Honeypot — hidden from people and assistive tech; bots that fill it are ignored. */}
      <div aria-hidden="true" className="absolute -left-[9999px] h-px w-px overflow-hidden">
        <label>
          Website
          <input type="text" name="website" tabIndex={-1} autoComplete="off" defaultValue="" />
        </label>
      </div>

      <div className="mt-1 flex flex-wrap items-center justify-between gap-4">
        <button
          type="submit"
          disabled={status === "sending"}
          className="flex h-14 w-full lift-on-hover cursor-pointer items-center justify-center gap-2.5 rounded-full bg-crimson px-[30px] text-base font-bold text-white hover:bg-crimson-hover disabled:cursor-wait disabled:opacity-80 md:w-auto md:shadow-crimson"
        >
          {status === "sending" ? copy.sending : copy.submit}
          <Icon name="send" size={18} className={cn(status === "sending" && "animate-pulse")} />
        </button>
        <span className="hidden text-[13.5px] text-slate md:inline">
          {copy.preferToTalk}{" "}
          <a href={site.contact.phoneHref} className="font-semibold text-crimson underline hover:text-crimson-hover">
            {site.contact.phoneDisplay}
          </a>
        </span>
      </div>
    </form>
  )
}
