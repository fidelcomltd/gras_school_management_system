import { photos } from "@/content/images"

/** Contact page copy, verbatim from the "Contact — Desktop" and "Contact — Mobile" artboards. */

export const contactHero = {
  breadcrumb: "Contact",
  title: "We'd love to hear from you.",
  text: "Questions about our Crèche, Preschool or Primary School? Send us a message, give us a call, or reach us on social media.",
  mobileText:
    "Questions about our Crèche, Preschool or Primary School? Send a message, call, or reach us on social media.",
}

export const contactDetails = {
  title: "Contact details",
  text: "Reach us directly on any of these.",
  photo: photos.toddlersLawn,
  labels: {
    call: "Call us",
    email: "Email us",
    tiktok: "TikTok",
    tiktokText: "Follow us on TikTok",
    facebook: "Facebook",
    facebookText: "Like our Facebook page",
    visit: "Visit us",
  },
}

export const contactForm = {
  title: "Send us a message",
  /** "Fields marked * are required." — the asterisk is rendered in crimson between the two parts. */
  required: { before: "Fields marked", after: "are required." },
  /** Desktop only, after the required note. */
  replyNote: "We'll get back to you as soon as possible.",
  errorSummary: "Please check the highlighted fields below.",
  mobileErrorSummary: "Please check the highlighted fields.",
  sendFailed: "We couldn't send your message just now. Please try again, or call us on",
  fields: {
    name: { label: "Full name", placeholder: "e.g. Ada Okafor", error: "Please enter your full name." },
    phone: { label: "Phone number", placeholder: "801 234 5678", error: "Please enter a valid phone number." },
    email: {
      label: "Email",
      optional: "(optional)",
      placeholder: "you@example.com",
      error: "Please enter a valid email address.",
    },
    topic: { label: "I'm enquiring about" },
    message: { label: "Message", placeholder: "How can we help?", error: "Please write a short message." },
    consent: {
      label: "I agree that Golden Royal Ark School may contact me by phone, SMS or email about this enquiry.",
      error: "Please tick the box so we can reply to you.",
    },
  },
  submit: "Send message",
  sending: "Sending…",
  preferToTalk: "Prefer to talk? Call",
  success: {
    title: (firstName: string) => `Thank you, ${firstName}!`,
    text: "Your message has been sent to Golden Royal Ark School. We'll get back to you as soon as possible.",
    again: "Send another message",
  },
}

export const resultHelp = {
  title: "Looking for your child's result?",
  text: "Results are on our secure result portal. Have your child's registration number and access PIN ready.",
  mobileText: "Have your child's registration number and access PIN ready.",
  cta: "Go to the Result Portal",
}

export const visit = {
  label: "Visit us",
  directions: "Get directions",
}
